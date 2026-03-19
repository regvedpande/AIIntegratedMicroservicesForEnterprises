using AiEnterprise.Core.Enums;

namespace AiEnterprise.Core.Models;

public class VendorRiskProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string VendorDomain { get; set; } = string.Empty;
    public string ServiceCategory { get; set; } = string.Empty; // e.g., "Cloud Infrastructure", "SaaS", "Payment Processing"
    public string Country { get; set; } = string.Empty;
    public RiskLevel CurrentRiskLevel { get; set; }
    public double CompositeRiskScore { get; set; }          // 0-100 (higher = riskier)
    public VendorRiskScoreBreakdown ScoreBreakdown { get; set; } = new();
    public List<string> DataTypesShared { get; set; } = new(); // e.g., ["PII", "Financial", "Healthcare"]
    public bool HasSignedDPA { get; set; }                  // Data Processing Agreement
    public bool HasSOC2 { get; set; }
    public bool HasISO27001 { get; set; }
    public DateTime? LastAssessmentDate { get; set; }
    public DateTime? NextAssessmentDueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class VendorRiskScoreBreakdown
{
    public double DataSecurityScore { get; set; }           // 0-100 (how secure their data handling is)
    public double ComplianceCertificationScore { get; set; } // 0-100 (certifications they hold)
    public double IncidentHistoryScore { get; set; }        // 0-100 (past breach history)
    public double ContractualProtectionScore { get; set; }  // 0-100 (contractual safeguards in place)
    public double GeographicRiskScore { get; set; }         // 0-100 (country/jurisdiction risk)
    public double FinancialStabilityScore { get; set; }     // 0-100 (financial health)
    public double AccessPrivilegeScore { get; set; }        // 0-100 (level of access granted)
}

public class BehavioralRiskEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public Guid? UserId { get; set; }
    public string EntityId { get; set; } = string.Empty;   // User, system, or IP that triggered event
    public string EntityType { get; set; } = string.Empty; // "User", "ServiceAccount", "ExternalIP"
    public string EventType { get; set; } = string.Empty;  // e.g., "LargeDataExport", "OffHoursAccess"
    public string Description { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public double AnomalyScore { get; set; }               // 0-100
    public string Metadata { get; set; } = string.Empty;  // JSON additional context
    public bool IsInvestigated { get; set; } = false;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
