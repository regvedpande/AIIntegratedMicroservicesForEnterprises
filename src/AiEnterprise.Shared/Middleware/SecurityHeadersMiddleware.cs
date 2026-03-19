using Microsoft.AspNetCore.Http;

namespace AiEnterprise.Shared.Middleware;

/// <summary>
/// Adds security headers to all responses to prevent common web attacks.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";
        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";
        // Enable XSS protection in browsers
        headers["X-XSS-Protection"] = "1; mode=block";
        // Strict transport security (1 year)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        // Referrer policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        // Permissions policy - restrict APIs not needed
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        // Remove server information
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        await _next(context);
    }
}
