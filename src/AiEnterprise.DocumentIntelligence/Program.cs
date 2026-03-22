using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.DocumentIntelligence.Services;
using AiEnterprise.Infrastructure.Database;
using AiEnterprise.Infrastructure.Extensions;
using AiEnterprise.Shared.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Document Intelligence API",
        Version = "v1",
        Description = "AI-powered document risk analysis using Claude. Analyzes contracts, DPAs, policies for compliance risks."
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured.\n" +
        "Development: run 'dotnet user-secrets set \"Jwt:Key\" \"<min-32-char-secret>\"' in this project directory.\n" +
        "Production: set the Jwt__Key environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AiEnterpriseGateway",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AiEnterprise",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHttpClient("gemini").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(90));

// Document Intelligence services
builder.Services.AddSingleton<GeminiDocumentAnalyzer>();
builder.Services.AddScoped<IDocumentAnalysisService, DocumentAnalysisService>();

var app = builder.Build();

// Initialize database schema on startup
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception in DocumentIntelligence");
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { error = ex?.Message ?? "Internal server error." }));
    });
});
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
