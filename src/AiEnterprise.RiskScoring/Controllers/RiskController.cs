using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiEnterprise.RiskScoring.Controllers;

[ApiController]
[Route("api/risk")]
[Authorize]
public class RiskController : ControllerBase
{
    private readonly IRiskScoringService _riskService;
    private readonly ILogger<RiskController> _logger;

    public RiskController(IRiskScoringService riskService, ILogger<RiskController> logger)
    {
        _riskService = riskService;
        _logger = logger;
    }

    /// <summary>
    /// Assess vendor risk across 7 dimensions: data security, certifications,
    /// incident history, contractual protections, jurisdiction, financial stability, and access level.
    /// Returns a composite risk score and top risk factors.
    /// </summary>
    [HttpPost("vendors/assess")]
    public async Task<ActionResult<VendorRiskSummary>> AssessVendor(
        [FromBody] VendorAssessmentRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.VendorName))
            return BadRequest(new { error = "VendorName is required." });

        var result = await _riskService.AssessVendorRiskAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get all vendor risk profiles for an enterprise, ordered by risk score (highest first).
    /// </summary>
    [HttpGet("{enterpriseId}/vendors")]
    public async Task<ActionResult<PagedResult<VendorRiskSummary>>> GetVendorProfiles(
        Guid enterpriseId,
        CancellationToken ct)
    {
        var profiles = await _riskService.GetVendorRiskProfilesAsync(enterpriseId, ct);
        return Ok(new PagedResult<VendorRiskSummary>(profiles, profiles.Count, 1, profiles.Count == 0 ? 1 : profiles.Count, 1));
    }

    /// <summary>
    /// Record a behavioral anomaly event for monitoring.
    /// Triggers automatic risk classification and scoring.
    /// Event types: MASS_DOWNLOAD, OFFHOURS_ACCESS, PRIVILEGE_ESCALATION, FAILED_AUTH, DATA_EXFILTRATION
    /// </summary>
    [HttpPost("behavioral/anomaly")]
    public async Task<ActionResult> RecordAnomaly(
        [FromBody] BehavioralAnomalyRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
            return BadRequest(new { error = "EventType is required." });

        var evt = await _riskService.RecordBehavioralAnomalyAsync(request, ct);
        return Ok(new
        {
            EventId = evt.Id,
            evt.RiskLevel,
            evt.AnomalyScore,
            evt.OccurredAt
        });
    }

    /// <summary>
    /// Get recent behavioral anomalies for an enterprise, ordered by anomaly score (most severe first).
    /// </summary>
    [HttpGet("{enterpriseId}/behavioral/recent")]
    public async Task<ActionResult<PagedResult<BehavioralRiskEvent>>> GetRecentAnomalies(
        Guid enterpriseId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit < 1 || limit > 500)
            return BadRequest(new { error = "Limit must be between 1 and 500." });

        var events = await _riskService.GetRecentAnomaliesAsync(enterpriseId, limit, ct);
        return Ok(new PagedResult<BehavioralRiskEvent>(events, events.Count, 1, events.Count == 0 ? 1 : events.Count, 1));
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "RiskScoring", Timestamp = DateTime.UtcNow });
}
