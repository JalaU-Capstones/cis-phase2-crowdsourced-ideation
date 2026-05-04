using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

/// <summary>
/// Health check that probes MySQL connectivity using a simple <c>SELECT 1</c>.
/// </summary>
public sealed class MySqlHealthCheck(IMySqlConnectionFactory connectionFactory, ILogger<MySqlHealthCheck> logger)
    : IMySqlHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = connectionFactory.Create();
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            _ = await cmd.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "MySQL health check failed.");
            return HealthCheckResult.Unhealthy("MySQL is unavailable.");
        }
    }
}

