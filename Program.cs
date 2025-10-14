// AiEnterprise.Gateway/Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Infrastructure.Configuration;
using AiEnterprise.Gateway.Middleware;
using AiEnterprise.Infrastructure.Caching;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AiEnterprise Gateway API", Version = "v1" });
    // JWT auth for Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Authentication (JWT)
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not found"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true
        };
    });

// Authorization
builder.Services.AddAuthorization();

// HttpClient for microservices (named clients)
builder.Services.AddHttpClient("AiService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:Services:AiServiceUrl"] ?? "http://localhost:5001");
});
builder.Services.AddHttpClient("AnalyticsService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:Services:AnalyticsServiceUrl"] ?? "http://localhost:5002");
});
builder.Services.AddHttpClient("EnterpriseService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:Services:EnterpriseServiceUrl"] ?? "http://localhost:5003");
});

// Register shared services (e.g., caching if needed)
builder.Services.AddSingleton<DapperContext>(); // If gateway needs DB access
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
});
builder.Services.AddScoped<RedisCacheService>();

// Infrastructure DI extensions
builder.Services.AddInfrastructureServices(builder.Configuration);

// Custom middleware
builder.Services.AddScoped<AuthMiddleware>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Use custom auth middleware (before controllers)
app.UseMiddleware<AuthMiddleware>();

app.MapControllers();

app.Run();