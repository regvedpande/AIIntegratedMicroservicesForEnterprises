using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiEnterprise.DocumentIntelligence.Services;

/// <summary>
/// Uses NVIDIA NIM (OpenAI-compatible API) to perform deep document risk analysis.
/// Identifies hidden contractual risks, compliance concerns, and provides actionable recommendations.
/// </summary>
public class NvidiaDocumentAnalyzer
{
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NvidiaDocumentAnalyzer> _logger;

    private const string NvidiaApiUrl = "https://integrate.api.nvidia.com/v1/chat/completions";
    private const string Model = "meta/llama-3.3-70b-instruct";

    public NvidiaDocumentAnalyzer(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<NvidiaDocumentAnalyzer> logger)
    {
        _apiKey = configuration["Nvidia:ApiKey"]
            ?? throw new InvalidOperationException("Nvidia:ApiKey is not configured.");
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Guid documentId,
        string documentContent,
        DocumentType documentType,
        string fileName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting NVIDIA NIM analysis of document {DocumentId} ({FileName})", documentId, fileName);

        var systemPrompt = BuildSystemPrompt(documentType);
        var userPrompt = BuildUserPrompt(documentContent, documentType, fileName);

        var requestBody = new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.1,
            max_tokens = 4096,
            stream = false
        };

        using var httpClient = _httpClientFactory.CreateClient("nvidia");
        using var request = new HttpRequestMessage(HttpMethod.Post, NvidiaApiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(requestBody);

        using var response = await httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("NVIDIA API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"NVIDIA API returned {response.StatusCode}: {errorBody}");
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<NvidiaApiResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from NVIDIA API.");

        var rawContent = apiResponse.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var tokensUsed = apiResponse.Usage?.TotalTokens ?? 0;

        _logger.LogInformation("NVIDIA NIM analysis complete for {DocumentId}. Tokens: {Tokens}", documentId, tokensUsed);

        return ParseAnalysisResponse(documentId, rawContent, tokensUsed);
    }

    private static string BuildSystemPrompt(DocumentType documentType) => documentType switch
    {
        DocumentType.Contract => """
            You are an expert enterprise legal and compliance analyst specializing in contract risk assessment.
            Identify: high-risk clauses (indemnity, liability caps, IP ownership, data rights), compliance gaps
            (GDPR, SOX, HIPAA, PCI-DSS), unfavorable terms, missing protections (audit rights, termination, SLA remedies),
            and data privacy obligations.
            You MUST respond with ONLY a valid JSON object — no markdown, no explanation, no ```json fences.
            """,

        DocumentType.DataProcessingAgreement => """
            You are an expert Data Protection Officer (DPO) and GDPR compliance specialist.
            Analyze DPAs for: missing GDPR Article 28 clauses, inadequate sub-processor controls,
            data subject rights gaps, cross-border transfer safeguards, breach notification procedures,
            and data retention/deletion obligations.
            You MUST respond with ONLY a valid JSON object — no markdown, no explanation, no ```json fences.
            """,

        DocumentType.Policy => """
            You are an enterprise governance and compliance expert.
            Analyze policies for: gaps against GDPR/SOX/HIPAA/ISO 27001, ambiguous language,
            missing responsibilities, inadequate incident response, and review cycle issues.
            You MUST respond with ONLY a valid JSON object — no markdown, no explanation, no ```json fences.
            """,

        _ => """
            You are an enterprise risk and compliance analyst.
            Analyze this business document to identify risks, compliance concerns, and provide actionable recommendations.
            You MUST respond with ONLY a valid JSON object — no markdown, no explanation, no ```json fences.
            """
    };

    private static string BuildUserPrompt(string content, DocumentType type, string fileName)
    {
        var truncated = content[..Math.Min(content.Length, 30000)];
        return $$"""
            Analyze this {{type}} document: "{{fileName}}"

            DOCUMENT CONTENT:
            {{truncated}}

            Respond in this EXACT JSON format (pure JSON only, no markdown):
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
            START YOUR RESPONSE WITH { AND END WITH }
            """;
    }

    private DocumentAnalysisResult ParseAnalysisResponse(Guid documentId, string rawContent, int tokensUsed)
    {
        try
        {
            var json = ExtractJson(rawContent);
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
                        ClauseReference = f.TryGetProperty("clauseReference", out var cr)
                            ? cr.GetString() ?? "Not specified"
                            : "Not specified",
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
            _logger.LogError(ex, "Failed to parse NVIDIA response for document {DocumentId}. Raw: {Raw}",
                documentId, rawContent[..Math.Min(rawContent.Length, 500)]);
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

    /// <summary>
    /// Robustly extracts a JSON object from a response that may contain markdown fences or leading text.
    /// </summary>
    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            return raw[start..(end + 1)];
        return raw.Trim();
    }

    private static List<string> ParseStringArray(JsonElement root, string propertyName)
    {
        var result = new List<string>();
        if (root.TryGetProperty(propertyName, out var arr))
            foreach (var item in arr.EnumerateArray())
                result.Add(item.GetString() ?? string.Empty);
        return result;
    }

    // ── NVIDIA API response DTOs ──────────────────────────────────────────────

    private sealed class NvidiaApiResponse
    {
        [JsonPropertyName("choices")] public List<NvidiaChoice>? Choices { get; set; }
        [JsonPropertyName("usage")] public NvidiaUsage? Usage { get; set; }
    }

    private sealed class NvidiaChoice
    {
        [JsonPropertyName("message")] public NvidiaMessage? Message { get; set; }
    }

    private sealed class NvidiaMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private sealed class NvidiaUsage
    {
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }
}
