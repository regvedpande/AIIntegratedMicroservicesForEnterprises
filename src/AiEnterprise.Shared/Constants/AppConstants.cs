namespace AiEnterprise.Shared.Constants;

public static class AppConstants
{
    public static class Services
    {
        public const string Gateway = "AiEnterprise.Gateway";
        public const string Compliance = "AiEnterprise.ComplianceService";
        public const string DocumentIntelligence = "AiEnterprise.DocumentIntelligence";
        public const string RiskScoring = "AiEnterprise.RiskScoring";
        public const string Audit = "AiEnterprise.AuditService";
        public const string Notification = "AiEnterprise.NotificationHub";
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string ComplianceOfficer = "ComplianceOfficer";
        public const string Analyst = "Analyst";
        public const string Viewer = "Viewer";
    }

    public static class CacheKeys
    {
        public static string EnterpriseFrameworks(Guid enterpriseId) => $"enterprise:{enterpriseId}:frameworks";
        public static string VendorRisk(Guid enterpriseId) => $"enterprise:{enterpriseId}:vendors";
        public static string ActiveAlerts(Guid enterpriseId) => $"enterprise:{enterpriseId}:alerts:active";
        public static string ComplianceScore(Guid enterpriseId, string framework) => $"enterprise:{enterpriseId}:compliance:{framework}";
    }

    public static class ClaimTypes
    {
        public const string EnterpriseId = "enterprise_id";
        public const string UserId = "user_id";
        public const string Role = "role";
        public const string Email = "email";
    }

    public static class HttpClients
    {
        public const string ComplianceService = "ComplianceService";
        public const string DocumentIntelligence = "DocumentIntelligence";
        public const string RiskScoring = "RiskScoring";
        public const string AuditService = "AuditService";
        public const string NotificationHub = "NotificationHub";
    }

    public static class DocumentLimits
    {
        public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
        public static readonly string[] AllowedMimeTypes =
        [
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/plain",
            "text/markdown"
        ];
    }
}
