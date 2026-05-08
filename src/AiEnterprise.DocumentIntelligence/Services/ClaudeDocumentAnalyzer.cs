using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Models;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiEnterprise.DocumentIntelligence.Services;

/// <summary>
/// Integrates with the Anthropic Claude API to perform deep document risk analysis.
/// Identifies hidden contractual risks, compliance concerns, and provides actionable
/// remediation recommendations for enterprise legal and compliance teams.
/// </summary>
public class ClaudeDocumentAnalyzer
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeDocumentAnalyzer> _logger;
    private const string Model = "claude-sonnet-4-6";

    public ClaudeDocumentAnalyzer(IConfiguration configuration, ILogger<ClaudeDocumentAnalyzer> logger)
    {
        var apiKey = configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException(
                "Anthropic:ApiKey is not configured.\n" +
                "Development: run 'dotnet user-secrets set \"Anthropic:ApiKey\" \"sk-ant-...\"' in this project directory.\n" +
                "Production: set the Anthropic__ApiKey environment variable.");

        _client = new AnthropicClient(apiKey);
        _logger = logger;
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Guid documentId,
        string documentContent,
        DocumentType documentType,
        string fileName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Claude analysis of document {DocumentId} ({FileName})", documentId, fileName);

        var systemPrompt = BuildSystemPrompt(documentType);
        var userPrompt = BuildUserPrompt(documentContent, documentType, fileName);

        var parameters = new MessageParameters
        {
            Model = Model,
            MaxTokens = 4096,
            Temperature = 0.1m,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userPrompt)]
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters, ct);

        var rawContent = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;
        var tokensUsed = inputTokens + outputTokens;

        _logger.LogInformation(
            "Claude analysis complete for {DocumentId}. Tokens: {Tokens} (in={In}, out={Out})",
            documentId, tokensUsed, inputTokens, outputTokens);

        return ParseAnalysisResponse(documentId, rawContent, tokensUsed);
    }

    private static string BuildSystemPrompt(DocumentType documentType) => documentType switch
    {
        DocumentType.Contract => """
            You are an expert enterprise legal and compliance analyst specialising in contract risk assessment.
            Identify: high-risk clauses (indemnity, liability caps, IP ownership, data rights), compliance gaps
            (GDPR, SOX, HIPAA, PCI-DSS), unfavourable terms, missing protections (audit rights, termination,
            SLA remedies), and data privacy obligations.
            Respond with a valid JSON object that exactly matches the schema in the user prompt — no markdown fences,
            no extra keys, no prose outside the JSON.
            """,

        DocumentType.DataProcessingAgreement => """
            You are an expert Data Protection Officer (DPO) and GDPR compliance specialist.
            Analyse DPAs for: missing GDPR Article 28 clauses, inadequate sub-processor controls,
            data subject rights gaps, cross-border transfer safeguards, breach notification procedures,
            and data retention/deletion obligations.
            Respond with a valid JSON object that exactly matches the schema in the user prompt — no markdown fences,
            no extra keys, no prose outside the JSON.
            """,

        DocumentType.Policy => """
            You are an enterprise governance and compliance expert.
            Analyse policies for: gaps against GDPR/SOX/HIPAA/ISO 27001, ambiguous language,
            missing responsibilities, inadequate incident response, and review-cycle deficiencies.
            Respond with a valid JSON object that exactly matches the schema in the user prompt — no markdown fences,
            no extra keys, no prose outside the JSON.
            """,

        _ => """
            You are an enterprise risk and compliance analyst.
            Analyse this business document to identify risks, compliance concerns, and provide
            actionable recommendations.
            Respond with a valid JSON object that exactly matches the schema in the user prompt — no markdown fences,
            no extra keys, no prose outside the JSON.
            """
    };

    private static string BuildUserPrompt(string content, DocumentType type, string fileName)
    {
        // Claude supports large context windows; we still cap to avoid runaway costs.
        var truncated = content[..Math.Min(content.Length, 80_000)];

        return $$"""
            Analyse this {{type}} document: "{{fileName}}"

            DOCUMENT CONTENT:
            {{truncated}}

            Respond in this EXACT JSON format (pure JSON, no markdown, no additional keys):
            {
              "overallRiskLevel": "Low|Medium|High|Critical",
              "riskScore": <integer 0-100>,
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

            Scoring guide: 0 = no risk, 100 = extreme risk. Include AT LEAST 3 findings when any risk exists.
            """;
    }

    private DocumentAnalysisResult ParseAnalysisResponse(Guid documentId, string rawContent, int tokensUsed)
    {
        try
        {
            // Strip any accidental markdown fences the model may still produce.
            var json = rawContent.Trim();
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('{');
                var end = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var riskLevel = ParseRiskLevel(root.GetProperty("overallRiskLevel").GetString() ?? "Medium");

            var findings = new List<DocumentRiskFinding>();
            if (root.TryGetProperty("findings", out var findingsEl))
            {
                foreach (var f in findingsEl.EnumerateArray())
                {
                    findings.Add(new DocumentRiskFinding
                    {
                        Category = f.GetProperty("category").GetString() ?? "General",
                        RiskLevel = ParseRiskLevel(f.GetProperty("riskLevel").GetString() ?? "Medium"),
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
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response for document {DocumentId}. Raw: {Raw}",
                documentId, rawContent.Length > 500 ? rawContent[..500] : rawContent);

            return new DocumentAnalysisResult
            {
                DocumentId = documentId,
                OverallRiskLevel = RiskLevel.Medium,
                RiskScore = 50,
                ExecutiveSummary = "Analysis completed but structured response parsing failed. Manual review is required.",
                RawAiResponse = rawContent,
                ModelUsed = Model,
                TokensUsed = tokensUsed,
                AnalyzedAt = DateTime.UtcNow
            };
        }
    }

    private static RiskLevel ParseRiskLevel(string value) => value switch
    {
        "Low" => RiskLevel.Low,
        "High" => RiskLevel.High,
        "Critical" => RiskLevel.Critical,
        _ => RiskLevel.Medium
    };

    private static List<string> ParseStringArray(System.Text.Json.JsonElement root, string propertyName)
    {
        var result = new List<string>();
        if (root.TryGetProperty(propertyName, out var arr))
            foreach (var item in arr.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s))
                    result.Add(s);
            }
        return result;
    }
}
