using AiEnterprise.Core.Enums;

namespace AiEnterprise.Core.Models;

public class ComplianceRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleCode { get; set; } = string.Empty;     // e.g., "GDPR-ART17", "SOX-302"
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceFramework Framework { get; set; }
    public ViolationSeverity DefaultSeverity { get; set; }
    public string Category { get; set; } = string.Empty;     // e.g., "Data Retention", "Access Control"
    public string EvaluationLogic { get; set; } = string.Empty; // JSON rule definition
    public string RemediationGuidance { get; set; } = string.Empty;
    public string RegulatoryReference { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ComplianceViolation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public ComplianceFramework Framework { get; set; }
    public ViolationSeverity Severity { get; set; }
    public ViolationStatus Status { get; set; } = ViolationStatus.Open;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AffectedResource { get; set; } = string.Empty; // e.g., "Customer PII Table", "API Endpoint /users"
    public string Evidence { get; set; } = string.Empty;         // JSON evidence data
    public string RemediationSteps { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public bool IsAiDetected { get; set; } = false;
    public double AiConfidenceScore { get; set; }            // 0-1 confidence
}

public class CompliancePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Guid> ApplicableRuleIds { get; set; } = new();
    public string PolicyDocument { get; set; } = string.Empty; // Markdown/HTML
    public string Version { get; set; } = "1.0";
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
