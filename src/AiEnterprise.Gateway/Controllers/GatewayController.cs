using AiEnterprise.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiEnterprise.Gateway.Controllers;

[ApiController]
[Route("api/gateway")]
[Authorize]
public class GatewayController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GatewayController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GatewayController(IHttpClientFactory httpClientFactory, ILogger<GatewayController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ─── Compliance Routes ────────────────────────────────────────────────────

    /// <summary>Run a compliance check for a specific framework and resource.</summary>
    [HttpPost("compliance/check")]
    public Task<ActionResult> ComplianceCheck([FromBody] ComplianceCheckRequest request, CancellationToken ct)
        => ForwardAsync("ComplianceService", HttpMethod.Post, "/api/compliance/check", request, ct);

    /// <summary>Get compliance framework summaries for an enterprise.</summary>
    [HttpGet("compliance/{enterpriseId}/frameworks")]
    public Task<ActionResult> GetFrameworks(Guid enterpriseId, CancellationToken ct)
        => ForwardAsync("ComplianceService", HttpMethod.Get, $"/api/compliance/{enterpriseId}/frameworks", null, ct);

    /// <summary>Get compliance violations for an enterprise.</summary>
    [HttpGet("compliance/{enterpriseId}/violations")]
    public Task<ActionResult> GetViolations(Guid enterpriseId, [FromQuery] string? status, CancellationToken ct)
        => ForwardAsync("ComplianceService", HttpMethod.Get, $"/api/compliance/{enterpriseId}/violations?status={status}", null, ct);

    /// <summary>Resolve a compliance violation.</summary>
    [HttpPatch("compliance/violations/{violationId}/resolve")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public Task<ActionResult> ResolveViolation(Guid violationId, [FromBody] ResolveViolationRequest request, CancellationToken ct)
        => ForwardAsync("ComplianceService", HttpMethod.Patch, $"/api/compliance/violations/{violationId}/resolve", request, ct);

    // ─── Document Intelligence Routes ────────────────────────────────────────

    /// <summary>Upload and AI-analyze a document for risks and compliance concerns.</summary>
    [HttpPost("documents/analyze")]
    public Task<ActionResult> AnalyzeDocument([FromBody] AnalyzeDocumentRequest request, CancellationToken ct)
        => ForwardAsync("DocumentIntelligence", HttpMethod.Post, "/api/documents/analyze", request, ct);

    /// <summary>Get AI analysis result for a document.</summary>
    [HttpGet("documents/{documentId}/analysis")]
    public Task<ActionResult> GetDocumentAnalysis(Guid documentId, CancellationToken ct)
        => ForwardAsync("DocumentIntelligence", HttpMethod.Get, $"/api/documents/{documentId}/analysis", null, ct);

    /// <summary>List documents for an enterprise.</summary>
    [HttpGet("documents/{enterpriseId}/list")]
    public Task<ActionResult> ListDocuments(Guid enterpriseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => ForwardAsync("DocumentIntelligence", HttpMethod.Get, $"/api/documents/{enterpriseId}/list?page={page}&pageSize={pageSize}", null, ct);

    // ─── Risk Scoring Routes ──────────────────────────────────────────────────

    /// <summary>Assess vendor risk across 7 dimensions.</summary>
    [HttpPost("risk/vendors/assess")]
    public Task<ActionResult> AssessVendorRisk([FromBody] VendorAssessmentRequest request, CancellationToken ct)
        => ForwardAsync("RiskScoring", HttpMethod.Post, "/api/risk/vendors/assess", request, ct);

    /// <summary>Get all vendor risk profiles for an enterprise.</summary>
    [HttpGet("risk/{enterpriseId}/vendors")]
    public Task<ActionResult> GetVendorProfiles(Guid enterpriseId, CancellationToken ct)
        => ForwardAsync("RiskScoring", HttpMethod.Get, $"/api/risk/{enterpriseId}/vendors", null, ct);

    /// <summary>Record a behavioral anomaly event.</summary>
    [HttpPost("risk/behavioral/anomaly")]
    public Task<ActionResult> RecordAnomaly([FromBody] BehavioralAnomalyRequest request, CancellationToken ct)
        => ForwardAsync("RiskScoring", HttpMethod.Post, "/api/risk/behavioral/anomaly", request, ct);

    // ─── Audit Routes ─────────────────────────────────────────────────────────

    /// <summary>Query the tamper-proof audit log.</summary>
    [HttpGet("audit/{enterpriseId}/query")]
    [Authorize(Roles = "Admin,ComplianceOfficer,Analyst")]
    public Task<ActionResult> QueryAuditLog(Guid enterpriseId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => ForwardAsync("AuditService", HttpMethod.Get, $"/api/audit/{enterpriseId}/query?from={from:O}&to={to:O}", null, ct);

    /// <summary>Generate a compliance report.</summary>
    [HttpPost("audit/report")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public Task<ActionResult> GenerateReport([FromBody] GenerateReportRequest request, CancellationToken ct)
        => ForwardAsync("AuditService", HttpMethod.Post, "/api/audit/report", request, ct);

    /// <summary>Verify integrity of an audit log entry (detect tampering).</summary>
    [HttpGet("audit/verify/{auditEntryId}")]
    [Authorize(Roles = "Admin,ComplianceOfficer")]
    public Task<ActionResult> VerifyAuditIntegrity(Guid auditEntryId, CancellationToken ct)
        => ForwardAsync("AuditService", HttpMethod.Get, $"/api/audit/verify/{auditEntryId}", null, ct);

    // ─── Notification Routes ──────────────────────────────────────────────────

    /// <summary>Get active alerts for an enterprise.</summary>
    [HttpGet("notifications/{enterpriseId}/active")]
    public Task<ActionResult> GetActiveAlerts(Guid enterpriseId, CancellationToken ct)
        => ForwardAsync("NotificationHub", HttpMethod.Get, $"/api/notifications/{enterpriseId}/active", null, ct);

    /// <summary>Acknowledge an alert.</summary>
    [HttpPatch("notifications/{alertId}/acknowledge")]
    public Task<ActionResult> AcknowledgeAlert(Guid alertId, [FromBody] object request, CancellationToken ct)
        => ForwardAsync("NotificationHub", HttpMethod.Patch, $"/api/notifications/{alertId}/acknowledge", request, ct);

    // ─── Health Check ─────────────────────────────────────────────────────────

    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<ActionResult> Health(CancellationToken ct)
    {
        var serviceChecks = new[]
        {
            ("ComplianceService", "/api/compliance/health"),
            ("DocumentIntelligence", "/api/documents/health"),
            ("RiskScoring", "/api/risk/health"),
            ("AuditService", "/api/audit/health"),
            ("NotificationHub", "/api/notifications/health"),
        };

        var statuses = new Dictionary<string, string>();
        await Parallel.ForEachAsync(serviceChecks, ct, async (check, token) =>
        {
            try
            {
                var client = _httpClientFactory.CreateClient(check.Item1);
                client.Timeout = TimeSpan.FromSeconds(3);
                var response = await client.GetAsync(check.Item2, token);
                statuses[check.Item1] = response.IsSuccessStatusCode ? "Healthy" : "Degraded";
            }
            catch
            {
                statuses[check.Item1] = "Unreachable";
            }
        });

        var allHealthy = statuses.Values.All(s => s == "Healthy");
        return StatusCode(allHealthy ? 200 : 207, new
        {
            Gateway = "Healthy",
            OverallStatus = allHealthy ? "Healthy" : "Degraded",
            Services = statuses,
            CheckedAt = DateTime.UtcNow
        });
    }

    // ─── Private forwarding helper ────────────────────────────────────────────

    private async Task<ActionResult> ForwardAsync(
        string clientName,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(clientName);

            // Forward the Authorization header to downstream services
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);

            HttpResponseMessage response;
            if (body is not null)
            {
                using var content = JsonContent.Create(body, options: JsonOptions);
                response = method == HttpMethod.Patch
                    ? await client.PatchAsync(path, content, ct)
                    : await client.PostAsync(path, content, ct);
            }
            else
            {
                response = await client.GetAsync(path, ct);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Downstream {ClientName} returned {StatusCode} for {Path}",
                    clientName, response.StatusCode, path);
            }

            return StatusCode((int)response.StatusCode, responseBody.Length > 0
                ? (object)JsonSerializer.Deserialize<object>(responseBody, JsonOptions)!
                : new { });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Service {ClientName} is unavailable", clientName);
            return StatusCode(503, new { error = $"{clientName} is currently unavailable. Please try again shortly." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { error = $"{clientName} request timed out." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error forwarding to {ClientName}", clientName);
            return StatusCode(500, new { error = "An internal error occurred." });
        }
    }
}
