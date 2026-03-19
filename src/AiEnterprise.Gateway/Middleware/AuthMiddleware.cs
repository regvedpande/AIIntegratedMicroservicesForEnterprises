using System.Security.Claims;

namespace AiEnterprise.Gateway.Middleware;

/// <summary>
/// Gateway API Key validation middleware.
/// All requests to the gateway must carry a valid X-Api-Key header in addition to a JWT token.
/// This dual-layer authentication prevents stolen JWT tokens from being used outside the gateway.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private readonly IConfiguration _configuration;

    // These paths bypass API key check (health probes should not require auth)
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/gateway/health",
        "/swagger",
        "/swagger/index.html"
    };

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow health checks and Swagger without API key
        if (PublicPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        var expectedKey = _configuration["Gateway:ApiKey"];

        if (string.IsNullOrEmpty(expectedKey))
        {
            _logger.LogCritical("Gateway:ApiKey is not configured! Blocking all requests for security.");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("Service configuration error.");
            return;
        }

        if (string.IsNullOrEmpty(apiKey) || !string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid or missing API key from IP {RemoteIp} for path {Path}",
                context.Connection.RemoteIpAddress, context.Request.Path);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized: Valid X-Api-Key header required.\"}");
            return;
        }

        // Log authenticated requests (not health checks, to avoid log noise)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var enterpriseId = context.User.FindFirst("enterprise_id")?.Value;
            _logger.LogDebug("Authenticated request: User={UserId}, Enterprise={EnterpriseId}, Path={Path}",
                userId, enterpriseId, context.Request.Path);
        }

        await _next(context);
    }
}
