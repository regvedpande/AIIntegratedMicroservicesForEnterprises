using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiEnterprise.NotificationHub.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(INotificationService notificationService, ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Send an alert. Intelligent deduplication prevents alert storms.
    /// High/Critical severity alerts are delivered via webhook + email.
    /// Low/Medium severity are queued for in-app delivery only.
    /// </summary>
    [HttpPost("alert")]
    public async Task<ActionResult> SendAlert([FromBody] Alert alert, CancellationToken ct)
    {
        if (alert.EnterpriseId == Guid.Empty)
            return BadRequest(new { error = "EnterpriseId is required." });

        if (string.IsNullOrWhiteSpace(alert.Title))
            return BadRequest(new { error = "Alert title is required." });

        await _notificationService.SendAlertAsync(alert, ct);
        return Ok(new { AlertId = alert.Id, message = "Alert processed." });
    }

    /// <summary>
    /// Get all active (unresolved) alerts for an enterprise, sorted by severity then date.
    /// </summary>
    [HttpGet("{enterpriseId}/active")]
    public async Task<ActionResult<IReadOnlyList<Alert>>> GetActiveAlerts(Guid enterpriseId, CancellationToken ct)
    {
        var alerts = await _notificationService.GetActiveAlertsAsync(enterpriseId, ct);
        return Ok(alerts);
    }

    /// <summary>
    /// Acknowledge an alert to signal it has been reviewed and is being handled.
    /// </summary>
    [HttpPatch("{alertId}/acknowledge")]
    public async Task<ActionResult> AcknowledgeAlert(Guid alertId, [FromBody] AcknowledgeRequest request, CancellationToken ct)
    {
        var success = await _notificationService.AcknowledgeAlertAsync(alertId, request.UserId, ct);
        if (!success) return NotFound(new { error = "Alert not found or already acknowledged." });
        return Ok(new { message = "Alert acknowledged." });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "NotificationHub", Timestamp = DateTime.UtcNow });
}

public record AcknowledgeRequest(Guid UserId);
