using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiEnterprise.ComplianceService.Controllers;

[ApiController]
[Route("api/compliance")]
[Authorize]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceService _complianceService;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(IComplianceService complianceService, ILogger<ComplianceController> logger)
    {
        _complianceService = complianceService;
        _logger = logger;
    }

    /// <summary>
    /// Run a compliance check against a specific resource for a given framework.
    /// </summary>
    [HttpPost("check")]
    public async Task<ActionResult<ComplianceCheckResult>> RunCheck(
        [FromBody] ComplianceCheckRequest request,
        CancellationToken ct)
    {
        if (request.EnterpriseId == Guid.Empty)
            return BadRequest(new { error = "EnterpriseId is required." });

        var result = await _complianceService.RunComplianceCheckAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get compliance framework summaries for an enterprise.
    /// </summary>
    [HttpGet("{enterpriseId}/frameworks")]
    public async Task<ActionResult<IReadOnlyList<ComplianceFrameworkSummary>>> GetFrameworkSummaries(
        Guid enterpriseId,
        CancellationToken ct)
    {
        var summaries = await _complianceService.GetFrameworkSummariesAsync(enterpriseId, ct);
        return Ok(summaries);
    }

    /// <summary>
    /// Get paginated violations for an enterprise.
    /// </summary>
    [HttpGet("{enterpriseId}/violations")]
    public async Task<ActionResult<PagedResult<ViolationSummaryDto>>> GetViolations(
        Guid enterpriseId,
        [FromQuery] ViolationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { error = "Page must be >= 1 and pageSize between 1-100." });

        var result = await _complianceService.GetViolationsAsync(enterpriseId, status, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific violation by ID.
    /// </summary>
    [HttpGet("violations/{violationId}")]
    public async Task<ActionResult> GetViolation(Guid violationId, CancellationToken ct)
    {
        var violation = await _complianceService.GetViolationAsync(violationId, ct);
        if (violation is null) return NotFound();
        return Ok(violation);
    }

    /// <summary>
    /// Resolve a compliance violation with remediation notes.
    /// </summary>
    [HttpPatch("violations/{violationId}/resolve")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public async Task<ActionResult> ResolveViolation(Guid violationId, [FromBody] ResolveViolationRequest request, CancellationToken ct)
    {
        if (request.ViolationId != violationId)
            return BadRequest(new { error = "ViolationId in URL and body must match." });

        var success = await _complianceService.ResolveViolationAsync(request, ct);
        if (!success) return NotFound();

        return Ok(new { message = "Violation resolved successfully." });
    }

    /// <summary>
    /// Get active compliance rules for a specific framework.
    /// </summary>
    [HttpGet("rules/{framework}")]
    public async Task<ActionResult> GetRules(ComplianceFramework framework, CancellationToken ct)
    {
        var rules = await _complianceService.GetActiveRulesAsync(framework, ct);
        return Ok(rules);
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "ComplianceService", Timestamp = DateTime.UtcNow });
}
