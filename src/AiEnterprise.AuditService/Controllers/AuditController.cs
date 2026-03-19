using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiEnterprise.AuditService.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditService auditService, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Log an auditable action. Called by other microservices to record tamper-proof audit entries.
    /// Each entry is hashed with SHA-256 for integrity verification.
    /// </summary>
    [HttpPost("log")]
    public async Task<ActionResult> LogEntry([FromBody] AuditEntry entry, CancellationToken ct)
    {
        if (entry.EnterpriseId == Guid.Empty)
            return BadRequest(new { error = "EnterpriseId is required." });

        await _auditService.LogAsync(entry, ct);
        return Ok(new { EntryId = entry.Id, message = "Audit entry recorded." });
    }

    /// <summary>
    /// Query the audit log with flexible filters.
    /// Supports filtering by action type, resource type, user, and date range.
    /// </summary>
    [HttpGet("{enterpriseId}/query")]
    public async Task<ActionResult<PagedResult<AuditEntrySummary>>> QueryLog(
        Guid enterpriseId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] AuditAction? action = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (from > to) return BadRequest(new { error = "From date must be before To date." });
        if (page < 1 || pageSize < 1 || pageSize > 200) return BadRequest(new { error = "Invalid pagination parameters." });

        var result = await _auditService.QueryAuditLogAsync(
            new AuditQueryRequest(enterpriseId, from, to, action, resourceType, userId, page, pageSize), ct);

        return Ok(result);
    }

    /// <summary>
    /// Generate a compliance report for a specific framework and period.
    /// </summary>
    [HttpPost("report")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public async Task<ActionResult<ComplianceReport>> GenerateReport(
        [FromBody] GenerateReportRequest request,
        CancellationToken ct)
    {
        if (request.PeriodStart > request.PeriodEnd)
            return BadRequest(new { error = "PeriodStart must be before PeriodEnd." });

        var report = await _auditService.GenerateComplianceReportAsync(request, ct);
        return Ok(report);
    }

    /// <summary>
    /// Verify the integrity of a specific audit log entry.
    /// Returns true if the entry has not been tampered with since it was recorded.
    /// </summary>
    [HttpGet("verify/{auditEntryId}")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public async Task<ActionResult> VerifyIntegrity(Guid auditEntryId, CancellationToken ct)
    {
        var isValid = await _auditService.VerifyIntegrityAsync(auditEntryId, ct);
        return Ok(new
        {
            AuditEntryId = auditEntryId,
            IsIntact = isValid,
            Message = isValid
                ? "Audit entry integrity verified - no tampering detected."
                : "CRITICAL: Audit entry integrity check FAILED - this entry may have been tampered with.",
            VerifiedAt = DateTime.UtcNow
        });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "AuditService", Timestamp = DateTime.UtcNow });
}
