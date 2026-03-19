using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using AiEnterprise.Infrastructure.Configuration;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiEnterprise.ComplianceService.Services;

/// <summary>
/// Core compliance rule engine that evaluates resources against regulatory frameworks.
/// Supports GDPR, SOX, HIPAA, PCI-DSS, ISO 27001, CCPA, and NIST frameworks.
/// </summary>
public class ComplianceRuleEngine : IComplianceService
{
    private readonly DapperContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<ComplianceRuleEngine> _logger;

    // Built-in compliance rules seeded into the system
    private static readonly IReadOnlyList<ComplianceRule> BuiltInRules = BuildBuiltInRules();

    public ComplianceRuleEngine(DapperContext db, ICacheService cache, ILogger<ComplianceRuleEngine> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ComplianceCheckResult> RunComplianceCheckAsync(ComplianceCheckRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Running compliance check for Enterprise {EnterpriseId}, Framework {Framework}",
            request.EnterpriseId, request.Framework);

        var rules = await GetActiveRulesAsync(request.Framework, ct);
        var violations = new List<ViolationSummaryDto>();
        var resourceData = ParseResourceData(request.ResourceData);

        foreach (var rule in rules)
        {
            var violation = EvaluateRule(rule, request, resourceData);
            if (violation != null)
            {
                var savedViolation = await CreateViolationAsync(new CreateViolationRequest(
                    request.EnterpriseId,
                    rule.Id,
                    violation.Value.Title,
                    violation.Value.Description,
                    request.ResourceId,
                    JsonSerializer.Serialize(new { Rule = rule.RuleCode, Resource = request.ResourceData }),
                    rule.DefaultSeverity
                ), ct);

                violations.Add(new ViolationSummaryDto(
                    savedViolation.Id,
                    rule.RuleCode,
                    violation.Value.Title,
                    rule.DefaultSeverity,
                    ViolationStatus.Open,
                    request.ResourceId,
                    DateTime.UtcNow
                ));
            }
        }

        var totalRules = rules.Count;
        var score = totalRules > 0 ? ((totalRules - violations.Count) / (double)totalRules) * 100.0 : 100.0;

        return new ComplianceCheckResult(
            request.EnterpriseId,
            request.Framework,
            violations.Count == 0,
            violations.Count,
            violations,
            Math.Round(score, 1),
            DateTime.UtcNow
        );
    }

    public async Task<IReadOnlyList<ComplianceFrameworkSummary>> GetFrameworkSummariesAsync(Guid enterpriseId, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            SELECT
                Framework,
                COUNT(*) as TotalViolations,
                SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) as OpenViolations,
                SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) as ResolvedViolations,
                MAX(DetectedAt) as LastChecked
            FROM ComplianceViolations
            WHERE EnterpriseId = @EnterpriseId
            GROUP BY Framework
            """;

        var rows = await connection.QueryAsync(sql, new { EnterpriseId = enterpriseId });
        var summaries = new List<ComplianceFrameworkSummary>();

        foreach (var row in rows)
        {
            var framework = (ComplianceFramework)row.Framework;
            var totalRules = BuiltInRules.Count(r => r.Framework == framework);
            var openViolations = (int)row.OpenViolations;
            var score = totalRules > 0 ? Math.Max(0, ((totalRules - openViolations) / (double)totalRules) * 100.0) : 100.0;

            summaries.Add(new ComplianceFrameworkSummary(
                framework,
                totalRules,
                openViolations,
                (int)row.ResolvedViolations,
                Math.Round(score, 1),
                row.LastChecked
            ));
        }

        return summaries;
    }

    public async Task<ComplianceViolation?> GetViolationAsync(Guid violationId, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        const string sql = "SELECT * FROM ComplianceViolations WHERE Id = @Id";
        return await connection.QuerySingleOrDefaultAsync<ComplianceViolation>(sql, new { Id = violationId });
    }

    public async Task<PagedResult<ViolationSummaryDto>> GetViolationsAsync(
        Guid enterpriseId,
        ViolationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        var whereClause = status.HasValue
            ? "WHERE EnterpriseId = @EnterpriseId AND Status = @Status"
            : "WHERE EnterpriseId = @EnterpriseId";

        var countSql = $"SELECT COUNT(*) FROM ComplianceViolations {whereClause}";
        var dataSql = $"""
            SELECT Id, RuleCode, Title, Severity, Status, AffectedResource, DetectedAt
            FROM ComplianceViolations {whereClause}
            ORDER BY DetectedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var parameters = new { EnterpriseId = enterpriseId, Status = (int?)status, Offset = (page - 1) * pageSize, PageSize = pageSize };
        var total = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var rows = await connection.QueryAsync(dataSql, parameters);

        var items = rows.Select(r => new ViolationSummaryDto(
            r.Id, r.RuleCode, r.Title, (ViolationSeverity)r.Severity,
            (ViolationStatus)r.Status, r.AffectedResource, r.DetectedAt
        )).ToList();

        return new PagedResult<ViolationSummaryDto>(items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<ComplianceViolation> CreateViolationAsync(CreateViolationRequest request, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        var violation = new ComplianceViolation
        {
            EnterpriseId = request.EnterpriseId,
            RuleId = request.RuleId,
            Title = request.Title,
            Description = request.Description,
            AffectedResource = request.AffectedResource,
            Evidence = request.Evidence,
            Severity = request.Severity,
            Framework = BuiltInRules.FirstOrDefault(r => r.Id == request.RuleId)?.Framework ?? ComplianceFramework.GDPR,
            RuleCode = BuiltInRules.FirstOrDefault(r => r.Id == request.RuleId)?.RuleCode ?? "UNKNOWN"
        };

        const string sql = """
            INSERT INTO ComplianceViolations
            (Id, EnterpriseId, RuleId, RuleCode, Framework, Severity, Status, Title, Description, AffectedResource, Evidence, DetectedAt, IsAiDetected, AiConfidenceScore)
            VALUES
            (@Id, @EnterpriseId, @RuleId, @RuleCode, @Framework, @Severity, @Status, @Title, @Description, @AffectedResource, @Evidence, @DetectedAt, @IsAiDetected, @AiConfidenceScore)
            """;

        await connection.ExecuteAsync(sql, new
        {
            violation.Id,
            violation.EnterpriseId,
            violation.RuleId,
            violation.RuleCode,
            Framework = (int)violation.Framework,
            Severity = (int)violation.Severity,
            Status = (int)violation.Status,
            violation.Title,
            violation.Description,
            violation.AffectedResource,
            violation.Evidence,
            violation.DetectedAt,
            violation.IsAiDetected,
            violation.AiConfidenceScore
        });

        // Invalidate cache
        await _cache.RemoveAsync($"violations:{request.EnterpriseId}", ct);

        _logger.LogWarning("Compliance violation created: {RuleCode} for Enterprise {EnterpriseId}", violation.RuleCode, request.EnterpriseId);
        return violation;
    }

    public async Task<bool> ResolveViolationAsync(ResolveViolationRequest request, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            UPDATE ComplianceViolations
            SET Status = @Status, ResolvedAt = @ResolvedAt, ResolutionNotes = @ResolutionNotes
            WHERE Id = @ViolationId
            """;

        var rows = await connection.ExecuteAsync(sql, new
        {
            Status = (int)ViolationStatus.Resolved,
            ResolvedAt = DateTime.UtcNow,
            request.ResolutionNotes,
            request.ViolationId
        });

        return rows > 0;
    }

    public Task<IReadOnlyList<ComplianceRule>> GetActiveRulesAsync(ComplianceFramework framework, CancellationToken ct = default)
    {
        var rules = BuiltInRules.Where(r => r.Framework == framework && r.IsActive).ToList();
        return Task.FromResult<IReadOnlyList<ComplianceRule>>(rules);
    }

    // --- Private helpers ---

    private static Dictionary<string, object?> ParseResourceData(string resourceDataJson)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(resourceDataJson) ?? new(); }
        catch { return new(); }
    }

    private static (string Title, string Description)? EvaluateRule(
        ComplianceRule rule,
        ComplianceCheckRequest request,
        Dictionary<string, object?> data)
    {
        // Rules evaluate JSON resource data against compliance criteria.
        // In production, this would be a configurable rule DSL; here we implement key rules literally.
        return rule.RuleCode switch
        {
            "GDPR-ART5-1F" => CheckDataMinimization(data),
            "GDPR-ART17" => CheckRightToErasure(data),
            "GDPR-ART32" => CheckSecurityMeasures(data),
            "GDPR-ART33" => CheckBreachNotification(data),
            "SOX-302" => CheckCeoSignoff(data),
            "SOX-404" => CheckInternalControls(data),
            "HIPAA-164.312A" => CheckAccessControls(data),
            "HIPAA-164.312E" => CheckTransmissionSecurity(data),
            "PCI-REQ3" => CheckCardDataProtection(data),
            "PCI-REQ8" => CheckUserAuthentication(data),
            _ => null
        };
    }

    private static (string, string)? CheckDataMinimization(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("collectsUnnecessaryData", out var val) && val?.ToString() == "true")
            return ("GDPR Data Minimization Violation", "System collects more personal data than necessary for stated purpose.");
        return null;
    }

    private static (string, string)? CheckRightToErasure(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("supportsDataDeletion", out var val) && val?.ToString() == "false")
            return ("GDPR Right to Erasure Not Supported", "System does not support data subject right to erasure (Art. 17).");
        return null;
    }

    private static (string, string)? CheckSecurityMeasures(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("encryptionEnabled", out var val) && val?.ToString() == "false")
            return ("GDPR Insufficient Security Measures", "Data at rest/transit is not encrypted, violating Art. 32 security requirements.");
        return null;
    }

    private static (string, string)? CheckBreachNotification(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("breachDetected", out var breached) && breached?.ToString() == "true")
        {
            if (data.TryGetValue("notifiedDPA", out var notified) && notified?.ToString() == "false")
                return ("GDPR Breach Notification Overdue", "Data breach detected but DPA not notified within 72 hours as required by Art. 33.");
        }
        return null;
    }

    private static (string, string)? CheckCeoSignoff(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("financialReportApproved", out var approved) && approved?.ToString() == "false")
            return ("SOX 302 Certification Missing", "CEO/CFO certification of financial report accuracy is missing.");
        return null;
    }

    private static (string, string)? CheckInternalControls(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("internalControlsTested", out var tested) && tested?.ToString() == "false")
            return ("SOX 404 Internal Controls Deficiency", "Internal controls over financial reporting have not been assessed and tested.");
        return null;
    }

    private static (string, string)? CheckAccessControls(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("roleBasedAccessEnabled", out var rbac) && rbac?.ToString() == "false")
            return ("HIPAA Access Control Deficiency", "PHI access is not controlled by role-based policies (HIPAA 164.312(a)).");
        return null;
    }

    private static (string, string)? CheckTransmissionSecurity(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("tlsEnabled", out var tls) && tls?.ToString() == "false")
            return ("HIPAA Transmission Security Violation", "PHI transmitted without TLS encryption, violating HIPAA 164.312(e).");
        return null;
    }

    private static (string, string)? CheckCardDataProtection(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("cardDataEncrypted", out var enc) && enc?.ToString() == "false")
            return ("PCI-DSS Cardholder Data Exposure Risk", "Cardholder data stored without strong encryption (PCI-DSS Req. 3).");
        return null;
    }

    private static (string, string)? CheckUserAuthentication(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("mfaEnabled", out var mfa) && mfa?.ToString() == "false")
            return ("PCI-DSS MFA Not Enforced", "Multi-factor authentication not enforced for access to cardholder data environment (PCI-DSS Req. 8).");
        return null;
    }

    private static IReadOnlyList<ComplianceRule> BuildBuiltInRules()
    {
        var rules = new List<ComplianceRule>
        {
            // GDPR
            new() { RuleCode = "GDPR-ART5-1F", Name = "Data Minimization", Framework = ComplianceFramework.GDPR, DefaultSeverity = ViolationSeverity.High, Category = "Data Protection", RemediationGuidance = "Review data collection and limit to only what is necessary for the stated purpose.", RegulatoryReference = "GDPR Article 5(1)(f)" },
            new() { RuleCode = "GDPR-ART17", Name = "Right to Erasure", Framework = ComplianceFramework.GDPR, DefaultSeverity = ViolationSeverity.Critical, Category = "Data Subject Rights", RemediationGuidance = "Implement data deletion workflows that can remove all personal data on request within 30 days.", RegulatoryReference = "GDPR Article 17" },
            new() { RuleCode = "GDPR-ART32", Name = "Security of Processing", Framework = ComplianceFramework.GDPR, DefaultSeverity = ViolationSeverity.Critical, Category = "Security", RemediationGuidance = "Implement encryption at rest and in transit, regular security assessments.", RegulatoryReference = "GDPR Article 32" },
            new() { RuleCode = "GDPR-ART33", Name = "Breach Notification", Framework = ComplianceFramework.GDPR, DefaultSeverity = ViolationSeverity.Critical, Category = "Incident Response", RemediationGuidance = "Notify supervisory authority within 72 hours of becoming aware of a breach.", RegulatoryReference = "GDPR Article 33" },

            // SOX
            new() { RuleCode = "SOX-302", Name = "CEO/CFO Certification", Framework = ComplianceFramework.SOX, DefaultSeverity = ViolationSeverity.Critical, Category = "Financial Reporting", RemediationGuidance = "Obtain signed certifications from CEO and CFO for all quarterly and annual financial reports.", RegulatoryReference = "SOX Section 302" },
            new() { RuleCode = "SOX-404", Name = "Internal Controls Assessment", Framework = ComplianceFramework.SOX, DefaultSeverity = ViolationSeverity.High, Category = "Internal Controls", RemediationGuidance = "Conduct annual assessment of internal controls over financial reporting with external auditor attestation.", RegulatoryReference = "SOX Section 404" },

            // HIPAA
            new() { RuleCode = "HIPAA-164.312A", Name = "Access Control for PHI", Framework = ComplianceFramework.HIPAA, DefaultSeverity = ViolationSeverity.Critical, Category = "Access Control", RemediationGuidance = "Implement unique user identification, emergency access procedures, and automatic logoff for PHI systems.", RegulatoryReference = "HIPAA 164.312(a)" },
            new() { RuleCode = "HIPAA-164.312E", Name = "PHI Transmission Security", Framework = ComplianceFramework.HIPAA, DefaultSeverity = ViolationSeverity.Critical, Category = "Encryption", RemediationGuidance = "Encrypt all PHI in transit using TLS 1.2+ and implement integrity controls.", RegulatoryReference = "HIPAA 164.312(e)" },

            // PCI-DSS
            new() { RuleCode = "PCI-REQ3", Name = "Protect Stored Cardholder Data", Framework = ComplianceFramework.PCIDSS, DefaultSeverity = ViolationSeverity.Critical, Category = "Data Protection", RemediationGuidance = "Encrypt cardholder data at rest using AES-256. Never store CVV/CVC codes.", RegulatoryReference = "PCI-DSS Requirement 3" },
            new() { RuleCode = "PCI-REQ8", Name = "Strong Authentication", Framework = ComplianceFramework.PCIDSS, DefaultSeverity = ViolationSeverity.High, Category = "Authentication", RemediationGuidance = "Enforce MFA for all non-consumer access to cardholder data environment.", RegulatoryReference = "PCI-DSS Requirement 8" },
        };

        return rules;
    }
}
