namespace AiEnterprise.Core.Enums;

public enum ComplianceFramework
{
    GDPR = 1,
    SOX = 2,
    HIPAA = 3,
    PCIDSS = 4,
    ISO27001 = 5,
    CCPA = 6,
    NIST = 7
}

public enum ViolationSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ViolationStatus
{
    Open = 1,
    InRemediation = 2,
    Resolved = 3,
    Accepted = 4,
    FalsePositive = 5
}

public enum RiskLevel
{
    Negligible = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum DocumentType
{
    Contract = 1,
    Policy = 2,
    Invoice = 3,
    DataProcessingAgreement = 4,
    NDA = 5,
    SLA = 6,
    PrivacyNotice = 7,
    AuditReport = 8,
    Other = 99
}

public enum NotificationChannel
{
    Email = 1,
    Webhook = 2,
    Slack = 3,
    Teams = 4,
    InApp = 5
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Accessed = 4,
    Exported = 5,
    Shared = 6,
    PolicyViolationDetected = 7,
    ComplianceCheckRun = 8,
    ReportGenerated = 9,
    AlertSent = 10,
    UserLogin = 11,
    UserLogout = 12,
    PermissionChanged = 13
}

public enum SubscriptionTier
{
    Starter = 1,
    Professional = 2,
    Enterprise = 3,
    EnterpriseElite = 4
}
