using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiEnterprise.DocumentIntelligence.Services;

/// <summary>
/// Uses Google Gemini 2.0 Flash to perform deep document risk analysis.
/// Identifies hidden contractual risks, compliance concerns, and provides actionable recommendations.
/// </summary>
public class GeminiDocumentAnalyzer
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GeminiDocumentAnalyzer> _logger;
    private const string Model = "gemini-2.0-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiDocumentAnalyzer(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<GeminiDocumentAnalyzer> logger)
    {
        _apiKey = configuration["Google:GeminiApiKey"]
            ?? throw new InvalidOperationException("Google:GeminiApiKey is not configured.");
        _http = httpClientFactory.CreateClient("gemini");
        _logger = logger;
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Guid documentId,
        string documentContent,
        DocumentType documentType,
        string fileName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Gemini analysis of document {DocumentId} ({FileName})", documentId, fileName);

        var systemPrompt = BuildSystemPrompt(documentType);
        var userPrompt = BuildUserPrompt(documentContent, documentType, fileName);

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 4096,
                responseMimeType = "application/json"
            }
        };

        var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
        var response = await _http.PostAsJsonAsync(url, requestBody, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini API error {Status}: {Body}", response.StatusCode, err);
            throw new InvalidOperationException($"Gemini API returned {response.StatusCode}: {err}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var candidates = doc.RootElement.GetProperty("candidates");
        var rawContent = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var tokensUsed = doc.RootElement.TryGetProperty("usageMetadata", out var usage)
            && usage.TryGetProperty("totalTokenCount", out var tc)
            ? tc.GetInt32() : 0;

        _logger.LogInformation("Gemini analysis complete for {DocumentId}. Tokens: {Tokens}", documentId, tokensUsed);

        return ParseAnalysisResponse(documentId, rawContent, tokensUsed);
    }

    private static string BuildSystemPrompt(DocumentType documentType) => documentType switch
    {
        DocumentType.Contract => """
            You are an expert enterprise legal and compliance analyst specializing in contract risk assessment.
            Identify: high-risk clauses (indemnity, liability caps, IP ownership, data rights), compliance gaps
            (GDPR, SOX, HIPAA, PCI-DSS), unfavorable terms, missing protections (audit rights, termination, SLA remedies),
            and data privacy obligations.
            Always respond with a valid JSON object following the exact schema provided.
            """,

        DocumentType.DataProcessingAgreement => """
            You are an expert Data Protection Officer (DPO) and GDPR compliance specialist.
            Analyze DPAs for: missing GDPR Article 28 clauses, inadequate sub-processor controls,
            data subject rights gaps, cross-border transfer safeguards, breach notification procedures,
            and data retention/deletion obligations.
            Always respond with a valid JSON object following the exact schema provided.
            """,

        DocumentType.Policy => """
            You are an enterprise governance and compliance expert.
            Analyze policies for: gaps against GDPR/SOX/HIPAA/ISO 27001, ambiguous language,
            missing responsibilities, inadequate incident response, and review cycle issues.
            Always respond with a valid JSON object following the exact schema provided.
            """,

        _ => """
            You are an enterprise risk and compliance analyst.
            Analyze this business document to identify risks, compliance concerns, and provide actionable recommendations.
            Always respond with a valid JSON object following the exact schema provided.
            """
    };

    private static string BuildUserPrompt(string content, DocumentType type, string fileName)
    {
        var truncated = content[..Math.Min(content.Length, 30000)];
        return $$"""
            Analyze this {{type}} document: "{{fileName}}"

            DOCUMENT CONTENT:
            {{truncated}}

            Respond in this EXACT JSON format (no markdown, pure JSON):
            {
              "overallRiskLevel": "Low|Medium|High|Critical",
              "riskScore": <number 0-100>,
              "executiveSummary": "<2-3 sentence executive summary>",
              "findings": [
                {
                  "category": "<category>",
                  "riskLevel": "Low|Medium|High|Critical",
                  "description": "<detailed description>",
                  "clauseReference": "<section/clause or 'Not specified'>",
                  "recommendation": "<specific remediation action>"
                }
              ],
              "keyClauses": ["<clause summary 1>", "<clause summary 2>"],
              "complianceConcerns": ["<concern referencing specific regulation>"],
              "recommendations": ["<priority recommendation 1>"]
            }

            Rules: riskScore 0=no risk 100=extreme risk. Include AT LEAST 3 findings if any risk exists.
            """;
    }

    private DocumentAnalysisResult ParseAnalysisResponse(Guid documentId, string rawContent, int tokensUsed)
    {
        try
        {
            var json = rawContent.Trim();
            if (json.StartsWith("```")) json = json[json.IndexOf('{')..];
            if (json.EndsWith("```")) json = json[..(json.LastIndexOf('}') + 1)];

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var riskLevel = (root.GetProperty("overallRiskLevel").GetString() ?? "Medium") switch
            {
                "Low" => RiskLevel.Low,
                "High" => RiskLevel.High,
                "Critical" => RiskLevel.Critical,
                _ => RiskLevel.Medium
            };

            var findings = new List<DocumentRiskFinding>();
            if (root.TryGetProperty("findings", out var findingsEl))
            {
                foreach (var f in findingsEl.EnumerateArray())
                {
                    findings.Add(new DocumentRiskFinding
                    {
                        Category = f.GetProperty("category").GetString() ?? "General",
                        RiskLevel = (f.GetProperty("riskLevel").GetString() ?? "Medium") switch
                        {
                            "Low" => RiskLevel.Low,
                            "High" => RiskLevel.High,
                            "Critical" => RiskLevel.Critical,
                            _ => RiskLevel.Medium
                        },
                        Description = f.GetProperty("description").GetString() ?? string.Empty,
                        ClauseReference = f.TryGetProperty("clauseReference", out var cr) ? cr.GetString() ?? "Not specified" : "Not specified",
                        Recommendation = f.GetProperty("recommendation").GetString() ?? string.Empty
                    });
                }
            }

            return new DocumentAnalysisResult
            {
                DocumentId = documentId,
                OverallRiskLevel = riskLevel,
                RiskScore = root.GetProperty("riskScore").GetDouble(),
                ExecutiveSummary = root.GetProperty("executiveSummary").GetString() ?? string.Empty,
                Findings = findings,
                KeyClauses = ParseStringArray(root, "keyClauses"),
                ComplianceConcerns = ParseStringArray(root, "complianceConcerns"),
                Recommendations = ParseStringArray(root, "recommendations"),
                RawAiResponse = rawContent,
                ModelUsed = Model,
                TokensUsed = tokensUsed,
                AnalyzedAt = DateTime.UtcNow
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini response for document {DocumentId}", documentId);
            return new DocumentAnalysisResult
            {
                DocumentId = documentId,
                OverallRiskLevel = RiskLevel.Medium,
                RiskScore = 50,
                ExecutiveSummary = "Analysis completed but response parsing failed. Manual review required.",
                RawAiResponse = rawContent,
                ModelUsed = Model,
                TokensUsed = tokensUsed,
                AnalyzedAt = DateTime.UtcNow
            };
        }
    }

    private static List<string> ParseStringArray(JsonElement root, string propertyName)
    {
        var result = new List<string>();
        if (root.TryGetProperty(propertyName, out var arr))
            foreach (var item in arr.EnumerateArray())
                result.Add(item.GetString() ?? string.Empty);
        return result;
    }
}
