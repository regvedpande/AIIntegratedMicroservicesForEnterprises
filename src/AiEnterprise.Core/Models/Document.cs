using AiEnterprise.Core.Enums;

namespace AiEnterprise.Core.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty; // Secure storage path
    public string ContentHash { get; set; } = string.Empty; // SHA-256 for integrity
    public DocumentAnalysisResult? AnalysisResult { get; set; }
    public bool IsAnalyzed { get; set; } = false;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedByUserId { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public string? Tags { get; set; } // JSON array of tags
}

public class DocumentAnalysisResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public RiskLevel OverallRiskLevel { get; set; }
    public double RiskScore { get; set; }            // 0-100
    public string ExecutiveSummary { get; set; } = string.Empty;
    public List<DocumentRiskFinding> Findings { get; set; } = new();
    public List<string> KeyClauses { get; set; } = new();
    public List<string> ComplianceConcerns { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string RawAiResponse { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = "claude-sonnet-4-6";
    public int TokensUsed { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class DocumentRiskFinding
{
    public string Category { get; set; } = string.Empty;   // e.g., "Indemnity", "Data Privacy", "IP Rights"
    public RiskLevel RiskLevel { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ClauseReference { get; set; } = string.Empty; // Section/clause reference
    public string Recommendation { get; set; } = string.Empty;
}
