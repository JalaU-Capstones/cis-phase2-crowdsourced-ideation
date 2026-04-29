using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

/// <summary>
/// Background service that periodically refreshes health statuses for MySQL and MongoDB.
/// </summary>
public sealed class DatabaseHealthMonitor(
    HealthStatusCache cache,
    MySqlHealthCheck mySqlHealthCheck,
    MongoDbHealthCheck mongoDbHealthCheck,
    IOptions<FallbackOptions> options,
    ILogger<DatabaseHealthMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(1, options.Value.HealthCheckIntervalSeconds);
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        // Run immediately on startup.
        await ProbeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await ProbeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown.
            }
        }
    }

    private async Task ProbeAsync(CancellationToken ct)
    {
        var mysql = await mySqlHealthCheck.CheckHealthAsync(new HealthCheckContext(), ct);
        var mongo = await mongoDbHealthCheck.CheckHealthAsync(new HealthCheckContext(), ct);

        var mysqlHealthy = mysql.Status == HealthStatus.Healthy;
        var mongoHealthy = mongo.Status == HealthStatus.Healthy;

        if (cache.TrySetMySqlHealthy(mysqlHealthy, out var mysqlTransition))
        {
            logger.LogWarning("MySQL health changed: {From} -> {To}", mysqlTransition!.Value.from, mysqlTransition.Value.to);
        }

        if (cache.TrySetMongoHealthy(mongoHealthy, out var mongoTransition))
        {
            logger.LogWarning("MongoDB health changed: {From} -> {To}", mongoTransition!.Value.from, mongoTransition.Value.to);
        }
    }
}

