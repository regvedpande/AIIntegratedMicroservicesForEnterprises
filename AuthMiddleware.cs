// AiEnterprise.Gateway/Middleware/AuthMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AiEnterprise.Gateway.Middleware;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Simple API Key check (fallback for non-JWT endpoints)
        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        var expectedKey = _configuration["Gateway:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
        {
            _logger.LogWarning("Invalid API key from {RemoteIp}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized: Invalid API Key");
            return;
        }

        // JWT is handled by AddAuthentication, but we can add custom claims if needed
        if (context.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("Authenticated user: {UserId} from {RemoteIp}",
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous",
                context.Connection.RemoteIpAddress);
        }

        await _next(context);
    }
}