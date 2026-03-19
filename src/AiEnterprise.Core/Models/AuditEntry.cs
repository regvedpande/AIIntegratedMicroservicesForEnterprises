using AiEnterprise.Core.Enums;

namespace AiEnterprise.Core.Models;

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string ResourceType { get; set; } = string.Empty; // e.g., "ComplianceRule", "Document", "User"
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;    // JSON before state
    public string NewValue { get; set; } = string.Empty;    // JSON after state
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty; // Which microservice logged this
    public string CorrelationId { get; set; } = string.Empty; // For tracing across services
    public string IntegrityHash { get; set; } = string.Empty; // SHA-256 of entry for tamper detection
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public class ComplianceReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ComplianceFramework Framework { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int HighViolations { get; set; }
    public int MediumViolations { get; set; }
    public int LowViolations { get; set; }
    public int ResolvedViolations { get; set; }
    public double ComplianceScore { get; set; }             // 0-100
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string DetailedFindings { get; set; } = string.Empty; // JSON
    public string Recommendations { get; set; } = string.Empty;
    public string ReportFormat { get; set; } = "JSON";      // JSON, PDF, HTML
    public Guid GeneratedByUserId { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
