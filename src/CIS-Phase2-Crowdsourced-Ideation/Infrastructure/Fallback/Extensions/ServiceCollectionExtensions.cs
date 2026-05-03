using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Routing;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Extensions;

/// <summary>
/// Service registrations for the emergency database fallback mechanism.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers fallback-related services (health checks, cache monitor, routing adapter, middleware dependencies).
    /// </summary>
    public static IServiceCollection AddDatabaseFallback(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FallbackOptions>(configuration.GetSection("Fallback"));

        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddSingleton<HealthStatusCache>();
        services.AddSingleton<IMySqlConnectionFactory, MySqlConnectionFactory>();
        services.AddSingleton<IMongoClientFactory, MongoClientFactory>();

        services.AddSingleton<MySqlHealthCheck>();
        services.AddSingleton<MongoDbHealthCheck>();
        services.AddHostedService<DatabaseHealthMonitor>();

        services.AddSingleton<IDatabaseFallbackService, DatabaseFallbackService>();

        // Keep underlying adapters available for delegation.
        services.AddScoped<MySqlAdapter>();
        services.AddScoped<MongoDbAdapter>();

        // Register underlying adapters as keyed IRepositoryAdapter instances so the routing adapter can
        // depend on IRepositoryAdapter without causing circular resolution.
        services.AddKeyedScoped<IRepositoryAdapter>("mysql", (sp, _) => sp.GetRequiredService<MySqlAdapter>());
        services.AddKeyedScoped<IRepositoryAdapter>("mongo", (sp, _) => sp.GetRequiredService<MongoDbAdapter>());

        // Fallback-aware adapter that delegates per request (resolved as the default IRepositoryAdapter).
        services.AddScoped<FallbackAdapter>();
        services.AddScoped<IRepositoryAdapter>(sp => sp.GetRequiredService<FallbackAdapter>());

        return services;
    }
}
