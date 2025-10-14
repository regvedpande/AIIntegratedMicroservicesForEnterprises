// AiEnterprise.Gateway/Controllers/GatewayController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Exceptions;
using AiEnterprise.Core.Interfaces.Services;

namespace AiEnterprise.Gateway.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // Require JWT auth
public class GatewayController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GatewayController> _logger;
    private readonly IConfiguration _configuration;

    public GatewayController(IHttpClientFactory httpClientFactory, ILogger<GatewayController> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Routes chat requests to AI Service
    /// </summary>
    [HttpPost("ai/chat")]
    public async Task<ActionResult<AiResponseDto>> Chat([FromBody] AiRequestDto request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AiService");
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/ai/chat", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("AI Service error: {Error}", error);
                return StatusCode((int)response.StatusCode, new { error });
            }

            var aiResponseJson = await response.Content.ReadAsStringAsync();
            var aiResponse = JsonSerializer.Deserialize<AiResponseDto>(aiResponseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return Ok(aiResponse);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error calling AI Service");
            return StatusCode(503, new { error = "AI Service unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in chat endpoint");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Routes analysis requests to Analytics Service
    /// </summary>
    [HttpPost("analytics/report")]
    public async Task<ActionResult<AnalyticsDto>> GetAnalytics([FromQuery] Guid enterpriseId, [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AnalyticsService");
            var response = await client.GetAsync($"/api/analytics?enterpriseId={enterpriseId}&start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Analytics Service error: {Error}", error);
                return StatusCode((int)response.StatusCode, new { error });
            }

            var analyticsJson = await response.Content.ReadAsStringAsync();
            var analytics = JsonSerializer.Deserialize<AnalyticsDto>(analyticsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return Ok(analytics);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error calling Analytics Service");
            return StatusCode(503, new { error = "Analytics Service unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in analytics endpoint");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Routes enterprise creation to Enterprise Service
    /// </summary>
    [HttpPost("enterprise/create")]
    public async Task<ActionResult<EnterpriseDto>> CreateEnterprise([FromBody] CreateEnterpriseRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("EnterpriseService");
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/enterprise", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Enterprise Service error: {Error}", error);
                return StatusCode((int)response.StatusCode, new { error });
            }

            var enterpriseJson = await response.Content.ReadAsStringAsync();
            var enterprise = JsonSerializer.Deserialize<EnterpriseDto>(enterpriseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return Ok(enterprise);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error calling Enterprise Service");
            return StatusCode(503, new { error = "Enterprise Service unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in enterprise endpoint");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Health check for gateway and downstream services
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Health()
    {
        var health = new
        {
            Gateway = "Healthy",
            Timestamp = DateTime.UtcNow,
            Services = new Dictionary<string, string>()
        };

        // Check AI Service
        try
        {
            var aiClient = _httpClientFactory.CreateClient("AiService");
            var aiResponse = await aiClient.GetAsync("/api/health");
            health.Services["AiService"] = aiResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy";
        }
        catch
        {
            health.Services["AiService"] = "Unreachable";
        }

        // Check Analytics Service
        try
        {
            var analyticsClient = _httpClientFactory.CreateClient("AnalyticsService");
            var analyticsResponse = await analyticsClient.GetAsync("/api/health");
            health.Services["AnalyticsService"] = analyticsResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy";
        }
        catch
        {
            health.Services["AnalyticsService"] = "Unreachable";
        }

        // Check Enterprise Service
        try
        {
            var enterpriseClient = _httpClientFactory.CreateClient("EnterpriseService");
            var enterpriseResponse = await enterpriseClient.GetAsync("/api/health");
            health.Services["EnterpriseService"] = enterpriseResponse.IsSuccessStatusCode ? "Healthy" : "Unhealthy";
        }
        catch
        {
            health.Services["EnterpriseService"] = "Unreachable";
        }

        return Ok(health);
    }
}

// DTO for requests (extend from Core if needed)
public record CreateEnterpriseRequest(string Name, string Domain, string SubscriptionTier = "Basic");