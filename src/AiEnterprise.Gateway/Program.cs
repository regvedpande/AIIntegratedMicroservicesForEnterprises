using AiEnterprise.Gateway.Middleware;
using AiEnterprise.Shared.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AiEnterprise ECRI Gateway",
        Version = "v1",
        Description = "Enterprise Compliance & Risk Intelligence API Gateway. " +
                      "Provides unified access to: Compliance Monitoring, AI Document Analysis, " +
                      "Vendor Risk Scoring, Tamper-proof Audit Trail, and Intelligent Alerting.",
        Contact = new OpenApiContact { Name = "AiEnterprise Platform" }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Enter 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Gateway API Key. Enter your API key.",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
            Array.Empty<string>()
        }
    });
});

// CORS - restrict to known origins in production (configurable per environment)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("EnterprisePolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        else
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    });
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. Use 'dotnet user-secrets set Jwt:Key <value>' or AIEP_JWT_KEY environment variable.");

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

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                ctx.Response.Headers["WWW-Authenticate"] = "Bearer error=\"invalid_token\"";
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ComplianceAccess", policy => policy.RequireRole("Admin", "ComplianceOfficer", "Analyst"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Rate limiting - protect against abuse and DoS
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit: 100 requests per minute per IP
    options.AddFixedWindowLimiter("GlobalLimit", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 100;
        limiterOptions.QueueLimit = 10;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Strict limit for document analysis (AI calls are expensive)
    options.AddFixedWindowLimiter("DocumentAnalysisLimit", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 10;
        limiterOptions.QueueLimit = 2;
    });

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"error\":\"Too many requests. Please retry after a moment.\",\"retryAfter\":60}", token);
    };
});

// Named HTTP clients for each downstream microservice
var services = builder.Configuration.GetSection("Services");
RegisterServiceClient(builder.Services, "ComplianceService", services["ComplianceServiceUrl"] ?? "http://localhost:5001");
RegisterServiceClient(builder.Services, "DocumentIntelligence", services["DocumentIntelligenceUrl"] ?? "http://localhost:5002");
RegisterServiceClient(builder.Services, "RiskScoring", services["RiskScoringUrl"] ?? "http://localhost:5003");
RegisterServiceClient(builder.Services, "AuditService", services["AuditServiceUrl"] ?? "http://localhost:5004");
RegisterServiceClient(builder.Services, "NotificationHub", services["NotificationHubUrl"] ?? "http://localhost:5005");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AiEnterprise ECRI Gateway v1"));
}

// Security middleware pipeline (order matters!)
app.UseMiddleware<SecurityHeadersMiddleware>();  // Security headers on all responses
app.UseHttpsRedirection();
app.UseCors("EnterprisePolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ApiKeyMiddleware>();           // API key check after auth (can access User claims)

app.MapControllers().RequireRateLimiting("GlobalLimit");

app.Run();

static void RegisterServiceClient(IServiceCollection services, string name, string baseUrl)
{
    services.AddHttpClient(name, client =>
    {
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "AiEnterprise-Gateway/1.0");
    });
}
