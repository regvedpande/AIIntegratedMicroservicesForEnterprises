using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Infrastructure.Database;
using AiEnterprise.Infrastructure.Extensions;
using AiEnterprise.NotificationHub.Services;
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
        Title = "Notification Hub API",
        Version = "v1",
        Description = "Intelligent alert routing with deduplication, priority scoring, and multi-channel delivery."
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
builder.Services.AddHttpClient();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddScoped<INotificationService, AlertNotificationService>();

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

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
