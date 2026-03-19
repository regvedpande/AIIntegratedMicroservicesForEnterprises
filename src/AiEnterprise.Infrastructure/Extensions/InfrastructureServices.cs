using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Infrastructure.Caching;
using AiEnterprise.Infrastructure.Configuration;
using AiEnterprise.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiEnterprise.Infrastructure.Extensions;

public static class InfrastructureServices
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddSingleton<DapperContext>();
        services.AddScoped<DatabaseInitializer>();

        // Caching
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis")
                ?? "localhost:6379";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }
}
