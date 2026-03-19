using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using AiEnterprise.Infrastructure.Configuration;
using AiEnterprise.Shared.Constants;
using AiEnterprise.Shared.Utilities;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AiEnterprise.DocumentIntelligence.Services;

public class DocumentAnalysisService : IDocumentAnalysisService
{
    private readonly DapperContext _db;
    private readonly ClaudeDocumentAnalyzer _claudeAnalyzer;
    private readonly ICacheService _cache;
    private readonly ILogger<DocumentAnalysisService> _logger;

    public DocumentAnalysisService(
        DapperContext db,
        ClaudeDocumentAnalyzer claudeAnalyzer,
        ICacheService cache,
        ILogger<DocumentAnalysisService> logger)
    {
        _db = db;
        _claudeAnalyzer = claudeAnalyzer;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DocumentAnalysisSummary> AnalyzeDocumentAsync(AnalyzeDocumentRequest request, CancellationToken ct = default)
    {
        // Validate file size from base64
        var estimatedBytes = request.Base64Content.Length * 3 / 4;
        if (estimatedBytes > AppConstants.DocumentLimits.MaxFileSizeBytes)
            throw new InvalidOperationException($"Document exceeds maximum size of {AppConstants.DocumentLimits.MaxFileSizeBytes / 1024 / 1024}MB.");

        var sanitizedFileName = SecurityUtility.SanitizeFileName(request.FileName);
        var contentBytes = Convert.FromBase64String(request.Base64Content);
        var contentHash = SecurityUtility.ComputeSha256Hash(request.Base64Content);
        var textContent = ExtractTextContent(contentBytes, request.DocumentType);

        // Save document record
        var document = new Document
        {
            EnterpriseId = request.EnterpriseId,
            FileName = sanitizedFileName,
            Type = request.DocumentType,
            FileSizeBytes = contentBytes.Length,
            ContentHash = contentHash,
            UploadedByUserId = request.UploadedByUserId,
            StoragePath = $"documents/{request.EnterpriseId}/{Guid.NewGuid()}/{sanitizedFileName}"
        };

        await SaveDocumentAsync(document);

        // Run Claude AI analysis
        var analysisResult = await _claudeAnalyzer.AnalyzeDocumentAsync(
            document.Id, textContent, request.DocumentType, sanitizedFileName, ct);

        await SaveAnalysisResultAsync(analysisResult);
        await UpdateDocumentAnalyzedAsync(document.Id, analysisResult.OverallRiskLevel);

        _logger.LogInformation("Document {DocumentId} analyzed. Risk: {RiskLevel}, Score: {Score}",
            document.Id, analysisResult.OverallRiskLevel, analysisResult.RiskScore);

        return new DocumentAnalysisSummary(
            document.Id,
            sanitizedFileName,
            request.DocumentType,
            analysisResult.OverallRiskLevel,
            analysisResult.RiskScore,
            analysisResult.ExecutiveSummary,
            analysisResult.Findings.Count,
            analysisResult.ComplianceConcerns.Count,
            analysisResult.AnalyzedAt
        );
    }

    public async Task<DocumentAnalysisResult?> GetAnalysisResultAsync(Guid documentId, CancellationToken ct = default)
    {
        var cacheKey = $"doc:analysis:{documentId}";
        var cached = await _cache.GetAsync<DocumentAnalysisResult>(cacheKey, ct);
        if (cached is not null) return cached;

        using var connection = _db.CreateConnection();
        const string sql = "SELECT * FROM DocumentAnalysisResults WHERE DocumentId = @DocumentId";
        var row = await connection.QuerySingleOrDefaultAsync(sql, new { DocumentId = documentId });

        if (row is null) return null;

        var result = new DocumentAnalysisResult
        {
            Id = row.Id,
            DocumentId = row.DocumentId,
            OverallRiskLevel = (RiskLevel)row.OverallRiskLevel,
            RiskScore = row.RiskScore,
            ExecutiveSummary = row.ExecutiveSummary,
            Findings = JsonSerializer.Deserialize<List<DocumentRiskFinding>>((string)row.Findings) ?? new List<DocumentRiskFinding>(),
            KeyClauses = JsonSerializer.Deserialize<List<string>>((string)row.KeyClauses) ?? new List<string>(),
            ComplianceConcerns = JsonSerializer.Deserialize<List<string>>((string)row.ComplianceConcerns) ?? new List<string>(),
            Recommendations = JsonSerializer.Deserialize<List<string>>((string)row.Recommendations) ?? new List<string>(),
            ModelUsed = row.ModelUsed,
            TokensUsed = row.TokensUsed,
            AnalyzedAt = row.AnalyzedAt
        };

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1), ct);
        return result;
    }

    public async Task<PagedResult<DocumentAnalysisSummary>> GetDocumentSummariesAsync(
        Guid enterpriseId, int page, int pageSize, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        const string countSql = "SELECT COUNT(*) FROM Documents WHERE EnterpriseId = @EnterpriseId";
        const string dataSql = """
            SELECT d.Id, d.FileName, d.Type, d.UploadedAt, dar.OverallRiskLevel, dar.RiskScore, dar.ExecutiveSummary, dar.AnalyzedAt,
                   (SELECT COUNT(*) FROM DocumentAnalysisResults WHERE DocumentId = d.Id) as HasAnalysis
            FROM Documents d
            LEFT JOIN DocumentAnalysisResults dar ON dar.DocumentId = d.Id
            WHERE d.EnterpriseId = @EnterpriseId
            ORDER BY d.UploadedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var parameters = new { EnterpriseId = enterpriseId, Offset = (page - 1) * pageSize, PageSize = pageSize };
        var total = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var rows = await connection.QueryAsync(dataSql, parameters);

        var items = rows.Select(r => new DocumentAnalysisSummary(
            r.Id, r.FileName, (DocumentType)r.Type,
            r.OverallRiskLevel != null ? (RiskLevel)r.OverallRiskLevel : RiskLevel.Low,
            r.RiskScore ?? 0.0,
            r.ExecutiveSummary ?? "Not yet analyzed",
            0, 0,
            r.AnalyzedAt ?? r.UploadedAt
        )).ToList();

        return new PagedResult<DocumentAnalysisSummary>(items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    private async Task SaveDocumentAsync(Document document)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            INSERT INTO Documents (Id, EnterpriseId, FileName, Type, FileSizeBytes, ContentHash, StoragePath, UploadedByUserId, UploadedAt)
            VALUES (@Id, @EnterpriseId, @FileName, @Type, @FileSizeBytes, @ContentHash, @StoragePath, @UploadedByUserId, @UploadedAt)
            """;
        await connection.ExecuteAsync(sql, new
        {
            document.Id, document.EnterpriseId, document.FileName,
            Type = (int)document.Type, document.FileSizeBytes, document.ContentHash,
            document.StoragePath, document.UploadedByUserId, document.UploadedAt
        });
    }

    private async Task SaveAnalysisResultAsync(DocumentAnalysisResult result)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            INSERT INTO DocumentAnalysisResults
            (Id, DocumentId, OverallRiskLevel, RiskScore, ExecutiveSummary, Findings, KeyClauses, ComplianceConcerns, Recommendations, ModelUsed, TokensUsed, AnalyzedAt)
            VALUES
            (@Id, @DocumentId, @OverallRiskLevel, @RiskScore, @ExecutiveSummary, @Findings, @KeyClauses, @ComplianceConcerns, @Recommendations, @ModelUsed, @TokensUsed, @AnalyzedAt)
            """;
        await connection.ExecuteAsync(sql, new
        {
            result.Id, result.DocumentId,
            OverallRiskLevel = (int)result.OverallRiskLevel,
            result.RiskScore, result.ExecutiveSummary,
            Findings = JsonSerializer.Serialize(result.Findings),
            KeyClauses = JsonSerializer.Serialize(result.KeyClauses),
            ComplianceConcerns = JsonSerializer.Serialize(result.ComplianceConcerns),
            Recommendations = JsonSerializer.Serialize(result.Recommendations),
            result.ModelUsed, result.TokensUsed, result.AnalyzedAt
        });
    }

    private async Task UpdateDocumentAnalyzedAsync(Guid documentId, RiskLevel riskLevel)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Documents SET IsAnalyzed = 1, AnalyzedAt = @AnalyzedAt WHERE Id = @Id",
            new { Id = documentId, AnalyzedAt = DateTime.UtcNow });
    }

    private static string ExtractTextContent(byte[] contentBytes, DocumentType documentType)
    {
        // In production, integrate with document parsers (iTextSharp for PDF, DocumentFormat.OpenXml for DOCX)
        // For now we handle plain text; PDF/DOCX parsing would be added per document type
        return Encoding.UTF8.GetString(contentBytes);
    }
}
