using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Models;

namespace AiEnterprise.Core.Interfaces.Services;

public interface IComplianceService
{
    Task<ComplianceCheckResult> RunComplianceCheckAsync(ComplianceCheckRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ComplianceFrameworkSummary>> GetFrameworkSummariesAsync(Guid enterpriseId, CancellationToken ct = default);
    Task<ComplianceViolation?> GetViolationAsync(Guid violationId, CancellationToken ct = default);
    Task<PagedResult<ViolationSummaryDto>> GetViolationsAsync(Guid enterpriseId, ViolationStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<ComplianceViolation> CreateViolationAsync(CreateViolationRequest request, CancellationToken ct = default);
    Task<bool> ResolveViolationAsync(ResolveViolationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ComplianceRule>> GetActiveRulesAsync(ComplianceFramework framework, CancellationToken ct = default);
}

public interface IDocumentAnalysisService
{
    Task<DocumentAnalysisSummary> AnalyzeDocumentAsync(AnalyzeDocumentRequest request, CancellationToken ct = default);
    Task<DocumentAnalysisResult?> GetAnalysisResultAsync(Guid documentId, CancellationToken ct = default);
    Task<PagedResult<DocumentAnalysisSummary>> GetDocumentSummariesAsync(Guid enterpriseId, int page, int pageSize, CancellationToken ct = default);
}

public interface IRiskScoringService
{
    Task<VendorRiskSummary> AssessVendorRiskAsync(VendorAssessmentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<VendorRiskSummary>> GetVendorRiskProfilesAsync(Guid enterpriseId, CancellationToken ct = default);
    Task<BehavioralRiskEvent> RecordBehavioralAnomalyAsync(BehavioralAnomalyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BehavioralRiskEvent>> GetRecentAnomaliesAsync(Guid enterpriseId, int limit, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
    Task<PagedResult<AuditEntrySummary>> QueryAuditLogAsync(AuditQueryRequest request, CancellationToken ct = default);
    Task<ComplianceReport> GenerateComplianceReportAsync(GenerateReportRequest request, CancellationToken ct = default);
    Task<bool> VerifyIntegrityAsync(Guid auditEntryId, CancellationToken ct = default);
}

public interface INotificationService
{
    Task SendAlertAsync(Alert alert, CancellationToken ct = default);
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(Guid enterpriseId, CancellationToken ct = default);
    Task<bool> AcknowledgeAlertAsync(Guid alertId, Guid userId, CancellationToken ct = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
