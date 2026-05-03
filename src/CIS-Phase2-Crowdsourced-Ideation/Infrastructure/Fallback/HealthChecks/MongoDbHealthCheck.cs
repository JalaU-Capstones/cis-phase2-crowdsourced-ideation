using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

/// <summary>
/// Health check that probes MongoDB connectivity using a <c>ping</c> command.
/// </summary>
public sealed class MongoDbHealthCheck(IMongoClientFactory clientFactory, ILogger<MongoDbHealthCheck> logger)
    : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = clientFactory.Create();
            var admin = client.GetDatabase("admin");
            await admin.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "MongoDB health check failed.");
            return HealthCheckResult.Unhealthy("MongoDB is unavailable.");
        }
    }
}

