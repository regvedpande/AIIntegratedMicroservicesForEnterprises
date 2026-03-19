using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AiEnterprise.DocumentIntelligence.Services;

/// <summary>
/// Uses Claude claude-sonnet-4-6 to perform deep document risk analysis.
/// Identifies hidden contractual risks, compliance concerns, and provides actionable recommendations.
/// This solves a critical enterprise gap: most companies spend weeks on manual contract review
/// that misses subtle risks. Claude can do this in seconds with consistent accuracy.
/// </summary>
public class ClaudeDocumentAnalyzer
{
    private readonly AnthropicClient _anthropic;
    private readonly ILogger<ClaudeDocumentAnalyzer> _logger;
    private const string ModelId = "claude-sonnet-4-6";

    public ClaudeDocumentAnalyzer(IConfiguration configuration, ILogger<ClaudeDocumentAnalyzer> logger)
    {
        var apiKey = configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured. Use dotnet user-secrets or ANTHROPIC_API_KEY environment variable.");
        _anthropic = new AnthropicClient(apiKey);
        _logger = logger;
    }

    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Guid documentId,
        string documentContent,
        DocumentType documentType,
        string fileName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting AI analysis of document {DocumentId} ({FileName})", documentId, fileName);

        var systemPrompt = BuildSystemPrompt(documentType);
        var userPrompt = BuildUserPrompt(documentContent, documentType, fileName);

        var request = new MessageParameters
        {
            Model = ModelId,
            MaxTokens = 4096,
            SystemMessage = systemPrompt,
            Messages =
            [
                new Message(RoleType.User, userPrompt)
            ]
        };

        var response = await _anthropic.Messages.GetClaudeMessageAsync(request, ct);
        var rawContent = response.Content.FirstOrDefault()?.ToString() ?? string.Empty;

        _logger.LogInformation("AI analysis complete for document {DocumentId}. Tokens used: {Tokens}",
            documentId, response.Usage?.OutputTokens ?? 0);

        return ParseAnalysisResponse(documentId, rawContent, response.Usage?.OutputTokens ?? 0);
    }

    private static string BuildSystemPrompt(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.Contract => """
                You are an expert enterprise legal and compliance analyst specializing in contract risk assessment.
                Your role is to analyze contracts for enterprises and identify:
                1. High-risk clauses (indemnity, limitation of liability, IP ownership, data rights)
                2. Compliance gaps (GDPR, SOX, HIPAA, PCI-DSS, CCPA where applicable)
                3. Unfavorable terms that could expose the enterprise to financial or legal risk
                4. Missing standard protections (audit rights, termination clauses, SLA remedies)
                5. Data privacy and security obligations

                Always respond with a valid JSON object following the exact schema provided.
                Be precise, cite specific clause locations when possible, and prioritize findings by risk severity.
                """,

            DocumentType.DataProcessingAgreement => """
                You are an expert Data Protection Officer (DPO) and GDPR compliance specialist.
                Analyze Data Processing Agreements (DPAs) to identify:
                1. Missing GDPR Article 28 mandatory clauses
                2. Inadequate sub-processor controls
                3. Data subject rights fulfillment gaps
                4. Cross-border transfer safeguards (SCCs, BCRs)
                5. Breach notification procedures
                6. Data retention and deletion obligations

                Always respond with a valid JSON object following the exact schema provided.
                """,

            DocumentType.Policy => """
                You are an enterprise governance and compliance expert.
                Analyze internal policies to identify:
                1. Gaps against regulatory requirements (GDPR, SOX, HIPAA, ISO 27001)
                2. Ambiguous language that could lead to inconsistent enforcement
                3. Missing employee obligations and responsibilities
                4. Inadequate incident response procedures
                5. Policy version control and review cycle issues

                Always respond with a valid JSON object following the exact schema provided.
                """,

            _ => """
                You are an enterprise risk and compliance analyst.
                Analyze this business document to identify risks, compliance concerns, and provide actionable recommendations.
                Always respond with a valid JSON object following the exact schema provided.
                """
        };
    }

    private static string BuildUserPrompt(string content, DocumentType type, string fileName)
    {
        var truncated = content[..Math.Min(content.Length, 30000)];
        return $$"""
            Analyze this {{type}} document: "{{fileName}}"

            DOCUMENT CONTENT:
            {{truncated}}

            Provide your analysis in this EXACT JSON format (no markdown, pure JSON):
            {
              "overallRiskLevel": "Low|Medium|High|Critical",
              "riskScore": <number 0-100>,
              "executiveSummary": "<2-3 sentence summary for executives>",
              "findings": [
                {
                  "category": "<category name>",
                  "riskLevel": "Low|Medium|High|Critical",
                  "description": "<detailed description>",
                  "clauseReference": "<section/clause reference or 'Not specified'>",
                  "recommendation": "<specific remediation action>"
                }
              ],
              "keyClauses": ["<important clause summary 1>", "<important clause summary 2>"],
              "complianceConcerns": ["<concern 1>", "<concern 2>"],
              "recommendations": ["<priority recommendation 1>", "<priority recommendation 2>"]
            }

            Rules:
            - riskScore: 0=no risk, 100=extreme risk
            - Include AT LEAST 3 findings if any risk exists
            - Be specific and actionable in recommendations
            - complianceConcerns should reference specific regulations (GDPR Art. X, SOX Sec. Y, etc.)
            """;
    }

    private DocumentAnalysisResult ParseAnalysisResponse(Guid documentId, string rawContent, int tokensUsed)
    {
        try
        {
            // Strip any accidental markdown code blocks
            var json = rawContent.Trim();
            if (json.StartsWith("```")) json = json[json.IndexOf('{')..];
            if (json.EndsWith("```")) json = json[..(json.LastIndexOf('}') + 1)];

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var riskLevelStr = root.GetProperty("overallRiskLevel").GetString() ?? "Medium";
            var riskLevel = riskLevelStr switch
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
                    var findingRisk = f.GetProperty("riskLevel").GetString() switch
                    {
                        "Low" => RiskLevel.Low,
                        "High" => RiskLevel.High,
                        "Critical" => RiskLevel.Critical,
                        _ => RiskLevel.Medium
                    };

                    findings.Add(new DocumentRiskFinding
                    {
                        Category = f.GetProperty("category").GetString() ?? "General",
                        RiskLevel = findingRisk,
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
                ModelUsed = ModelId,
                TokensUsed = tokensUsed,
                AnalyzedAt = DateTime.UtcNow
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response as JSON for document {DocumentId}", documentId);
            return new DocumentAnalysisResult
            {
                DocumentId = documentId,
                OverallRiskLevel = RiskLevel.Medium,
                RiskScore = 50,
                ExecutiveSummary = "Analysis completed but response parsing failed. Manual review required.",
                RawAiResponse = rawContent,
                ModelUsed = ModelId,
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
