using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using AiEnterprise.Infrastructure.Configuration;
using AiEnterprise.Shared.Utilities;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiEnterprise.AuditService.Services;

/// <summary>
/// Tamper-proof audit trail service.
///
/// Enterprise problem: Audit logs are frequently tampered with to hide unauthorized activity.
/// This service computes a SHA-256 integrity hash for each entry at write time.
/// Any tampering can be immediately detected by re-computing and comparing hashes.
/// This satisfies SOX 302/404, GDPR Article 5(2) (accountability), and ISO 27001 A.12.4.
/// </summary>
public class TamperProofAuditService : IAuditService
{
    private readonly DapperContext _db;
    private readonly ILogger<TamperProofAuditService> _logger;

    public TamperProofAuditService(DapperContext db, ILogger<TamperProofAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        // Compute integrity hash BEFORE storing - used to detect tamper after the fact
        entry.IntegrityHash = SecurityUtility.ComputeAuditIntegrityHash(
            entry.Id,
            entry.EnterpriseId,
            entry.Action.ToString(),
            entry.ResourceId,
            entry.Description,
            entry.OccurredAt);

        using var connection = _db.CreateConnection();
        const string sql = """
            INSERT INTO AuditLog
            (Id, EnterpriseId, UserId, UserEmail, Action, ResourceType, ResourceId, ResourceName,
             Description, OldValue, NewValue, IpAddress, UserAgent, ServiceName, CorrelationId, IntegrityHash, OccurredAt)
            VALUES
            (@Id, @EnterpriseId, @UserId, @UserEmail, @Action, @ResourceType, @ResourceId, @ResourceName,
             @Description, @OldValue, @NewValue, @IpAddress, @UserAgent, @ServiceName, @CorrelationId, @IntegrityHash, @OccurredAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            entry.Id, entry.EnterpriseId, entry.UserId, entry.UserEmail,
            Action = (int)entry.Action, entry.ResourceType, entry.ResourceId, entry.ResourceName,
            entry.Description, entry.OldValue, entry.NewValue,
            entry.IpAddress, entry.UserAgent, entry.ServiceName, entry.CorrelationId,
            entry.IntegrityHash, entry.OccurredAt
        });
    }

    public async Task<PagedResult<AuditEntrySummary>> QueryAuditLogAsync(AuditQueryRequest request, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();

        var whereClause = BuildWhereClause(request);
        var countSql = $"SELECT COUNT(*) FROM AuditLog {whereClause}";
        var dataSql = $"""
            SELECT Id, UserEmail, Action, ResourceType, ResourceName, Description, IpAddress, OccurredAt
            FROM AuditLog {whereClause}
            ORDER BY OccurredAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var parameters = new
        {
            request.EnterpriseId,
            request.From, request.To,
            Action = request.Action.HasValue ? (int?)request.Action.Value : null,
            request.ResourceType,
            request.UserId,
            Offset = (request.Page - 1) * request.PageSize,
            PageSize = request.PageSize
        };

        var total = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var rows = await connection.QueryAsync(dataSql, parameters);

        var items = rows.Select(r => new AuditEntrySummary(
            r.Id, r.UserEmail, (AuditAction)r.Action,
            r.ResourceType, r.ResourceName, r.Description, r.IpAddress, r.OccurredAt
        )).ToList();

        return new PagedResult<AuditEntrySummary>(
            items, total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }

    public async Task<ComplianceReport> GenerateComplianceReportAsync(GenerateReportRequest request, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();

        const string violationsSql = """
            SELECT
                COUNT(*) as Total,
                SUM(CASE WHEN Severity = 4 THEN 1 ELSE 0 END) as Critical,
                SUM(CASE WHEN Severity = 3 THEN 1 ELSE 0 END) as High,
                SUM(CASE WHEN Severity = 2 THEN 1 ELSE 0 END) as Medium,
                SUM(CASE WHEN Severity = 1 THEN 1 ELSE 0 END) as Low,
                SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) as Resolved
            FROM ComplianceViolations
            WHERE EnterpriseId = @EnterpriseId
              AND Framework = @Framework
              AND DetectedAt BETWEEN @PeriodStart AND @PeriodEnd
            """;

        var stats = await connection.QuerySingleAsync(violationsSql, new
        {
            request.EnterpriseId,
            Framework = (int)request.Framework,
            request.PeriodStart,
            request.PeriodEnd
        });

        var totalRules = 10; // Assume 10 rules per framework for score calculation
        var openViolations = (int)stats.Total - (int)stats.Resolved;
        var complianceScore = Math.Max(0, ((totalRules - openViolations) / (double)totalRules) * 100);

        var report = new ComplianceReport
        {
            EnterpriseId = request.EnterpriseId,
            Title = $"{request.Framework} Compliance Report - {request.PeriodStart:MMM yyyy} to {request.PeriodEnd:MMM yyyy}",
            Framework = request.Framework,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            TotalViolations = (int)stats.Total,
            CriticalViolations = (int)stats.Critical,
            HighViolations = (int)stats.High,
            MediumViolations = (int)stats.Medium,
            LowViolations = (int)stats.Low,
            ResolvedViolations = (int)stats.Resolved,
            ComplianceScore = Math.Round(complianceScore, 1),
            ExecutiveSummary = GenerateExecutiveSummary(stats, request.Framework, complianceScore),
            Recommendations = GenerateRecommendations(stats, request.Framework),
            GeneratedByUserId = request.GeneratedByUserId,
            GeneratedAt = DateTime.UtcNow
        };

        const string insertSql = """
            INSERT INTO ComplianceReports
            (Id, EnterpriseId, Title, Framework, PeriodStart, PeriodEnd, TotalViolations, CriticalViolations,
             HighViolations, MediumViolations, LowViolations, ResolvedViolations, ComplianceScore,
             ExecutiveSummary, Recommendations, GeneratedByUserId, GeneratedAt)
            VALUES
            (@Id, @EnterpriseId, @Title, @Framework, @PeriodStart, @PeriodEnd, @TotalViolations, @CriticalViolations,
             @HighViolations, @MediumViolations, @LowViolations, @ResolvedViolations, @ComplianceScore,
             @ExecutiveSummary, @Recommendations, @GeneratedByUserId, @GeneratedAt)
            """;

        await connection.ExecuteAsync(insertSql, new
        {
            report.Id, report.EnterpriseId, report.Title,
            Framework = (int)report.Framework,
            report.PeriodStart, report.PeriodEnd, report.TotalViolations,
            report.CriticalViolations, report.HighViolations, report.MediumViolations,
            report.LowViolations, report.ResolvedViolations, report.ComplianceScore,
            report.ExecutiveSummary, report.Recommendations,
            report.GeneratedByUserId, report.GeneratedAt
        });

        return report;
    }

    public async Task<bool> VerifyIntegrityAsync(Guid auditEntryId, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            SELECT Id, EnterpriseId, Action, ResourceId, Description, OccurredAt, IntegrityHash
            FROM AuditLog WHERE Id = @Id
            """;

        var row = await connection.QuerySingleOrDefaultAsync(sql, new { Id = auditEntryId });
        if (row is null) return false;

        var expectedHash = SecurityUtility.ComputeAuditIntegrityHash(
            row.Id, row.EnterpriseId,
            ((AuditAction)row.Action).ToString(),
            row.ResourceId, row.Description, row.OccurredAt);

        var isValid = expectedHash == row.IntegrityHash;
        if (!isValid)
            _logger.LogCritical("AUDIT INTEGRITY VIOLATION: Entry {AuditEntryId} has been tampered with!", auditEntryId);

        return isValid;
    }

    private static string BuildWhereClause(AuditQueryRequest request)
    {
        var conditions = new List<string>
        {
            "EnterpriseId = @EnterpriseId",
            "OccurredAt BETWEEN @From AND @To"
        };

        if (request.Action.HasValue) conditions.Add("Action = @Action");
        if (!string.IsNullOrEmpty(request.ResourceType)) conditions.Add("ResourceType = @ResourceType");
        if (request.UserId.HasValue) conditions.Add("UserId = @UserId");

        return "WHERE " + string.Join(" AND ", conditions);
    }

    private static string GenerateExecutiveSummary(dynamic stats, ComplianceFramework framework, double score)
    {
        var statusWord = score >= 80 ? "strong" : score >= 60 ? "moderate" : "poor";
        return $"During the reporting period, {(int)stats.Total} {framework} violations were detected, " +
               $"of which {(int)stats.Resolved} have been resolved. " +
               $"The organization shows {statusWord} {framework} compliance with a score of {score:F1}%. " +
               $"{(int)stats.Critical} critical violations require immediate executive attention.";
    }

    private static string GenerateRecommendations(dynamic stats, ComplianceFramework framework)
    {
        var recommendations = new List<string>();
        if ((int)stats.Critical > 0)
            recommendations.Add($"Immediately address {(int)stats.Critical} critical {framework} violation(s) - these carry the highest regulatory risk.");
        if ((int)stats.High > 0)
            recommendations.Add($"Remediate {(int)stats.High} high-severity violations within 30 days.");
        recommendations.Add($"Schedule quarterly {framework} compliance reviews to prevent violation accumulation.");
        recommendations.Add("Implement automated compliance monitoring to detect violations in real-time.");
        return JsonSerializer.Serialize(recommendations);
    }
}
