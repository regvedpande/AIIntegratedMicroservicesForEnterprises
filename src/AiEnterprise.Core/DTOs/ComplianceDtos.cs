using AiEnterprise.Core.Enums;

namespace AiEnterprise.Core.DTOs;

// --- Compliance ---

public record ComplianceCheckRequest(
    Guid EnterpriseId,
    ComplianceFramework Framework,
    string ResourceType,
    string ResourceId,
    string ResourceData  // JSON of the resource to check
);

public record ComplianceCheckResult(
    Guid EnterpriseId,
    ComplianceFramework Framework,
    bool IsCompliant,
    int ViolationsFound,
    IReadOnlyList<ViolationSummaryDto> Violations,
    double ComplianceScore,
    DateTime CheckedAt
);

public record ViolationSummaryDto(
    Guid ViolationId,
    string RuleCode,
    string Title,
    ViolationSeverity Severity,
    ViolationStatus Status,
    string AffectedResource,
    DateTime DetectedAt
);

public record CreateViolationRequest(
    Guid EnterpriseId,
    Guid RuleId,
    string Title,
    string Description,
    string AffectedResource,
    string Evidence,
    ViolationSeverity Severity
);

public record ResolveViolationRequest(
    Guid ViolationId,
    Guid ResolvedByUserId,
    string ResolutionNotes
);

public record ComplianceFrameworkSummary(
    ComplianceFramework Framework,
    int TotalRules,
    int ViolationsOpen,
    int ViolationsResolved,
    double ComplianceScore,
    DateTime LastChecked
);

// --- Document Intelligence ---

public record AnalyzeDocumentRequest(
    Guid EnterpriseId,
    Guid UploadedByUserId,
    DocumentType DocumentType,
    string FileName,
    string Base64Content    // Base64 encoded document content
);

public record DocumentAnalysisSummary(
    Guid DocumentId,
    string FileName,
    DocumentType Type,
    RiskLevel OverallRiskLevel,
    double RiskScore,
    string ExecutiveSummary,
    int FindingsCount,
    int ComplianceConcernsCount,
    DateTime AnalyzedAt
);

// --- Risk Scoring ---

public record VendorAssessmentRequest(
    Guid EnterpriseId,
    string VendorName,
    string VendorDomain,
    string ServiceCategory,
    string Country,
    List<string> DataTypesShared,
    bool HasSignedDPA,
    bool HasSOC2,
    bool HasISO27001,
    string AdditionalContext
);

public record VendorRiskSummary(
    Guid VendorId,
    string VendorName,
    RiskLevel RiskLevel,
    double CompositeRiskScore,
    string ServiceCategory,
    DateTime? LastAssessmentDate,
    List<string> TopRisks
);

public record BehavioralAnomalyRequest(
    Guid EnterpriseId,
    string EntityId,
    string EntityType,
    string EventType,
    string Description,
    string Metadata
);

// --- Audit ---

public record AuditQueryRequest(
    Guid EnterpriseId,
    DateTime From,
    DateTime To,
    AuditAction? Action = null,
    string? ResourceType = null,
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 50
);

public record AuditEntrySummary(
    Guid Id,
    string UserEmail,
    AuditAction Action,
    string ResourceType,
    string ResourceName,
    string Description,
    string IpAddress,
    DateTime OccurredAt
);

public record GenerateReportRequest(
    Guid EnterpriseId,
    Guid GeneratedByUserId,
    ComplianceFramework Framework,
    DateTime PeriodStart,
    DateTime PeriodEnd
);

// --- Shared ---

public record EnterpriseDto(
    Guid Id,
    string Name,
    string Domain,
    string SubscriptionTier,
    string Industry,
    bool IsActive,
    DateTime CreatedAt
);

public record AiRequestDto(
    string Prompt,
    string Context,
    Guid EnterpriseId,
    string RequestType  // "DocumentAnalysis", "ComplianceCheck", "RiskAssessment"
);

public record AiResponseDto(
    string Content,
    string ModelUsed,
    int TokensUsed,
    DateTime RespondedAt
);

public record AnalyticsDto(
    Guid EnterpriseId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int TotalViolations,
    int DocumentsAnalyzed,
    int VendorsAssessed,
    int AlertsSent,
    double AverageComplianceScore,
    Dictionary<string, int> ViolationsByFramework,
    Dictionary<string, int> ViolationsBySeverity
);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
