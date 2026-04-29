using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback;

/// <summary>
/// Implements Phase 1 compatible database fallback decisions using cached health statuses.
/// </summary>
public sealed class DatabaseFallbackService(
    HealthStatusCache health,
    IOptions<FallbackOptions> options,
    ILogger<DatabaseFallbackService> logger) : IDatabaseFallbackService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, (DatabaseType active, bool fallbackActive)> _last = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool IsFallbackActiveForVersion(string versionPath)
    {
        var db = GetActiveDatabase(versionPath);
        if (db is DatabaseType.BothDown)
            return false;

        var def = GetDefaultDatabase(versionPath);
        return options.Value.Enabled && db != def;
    }

    /// <inheritdoc />
    public DatabaseType GetActiveDatabase(string versionPath)
    {
        // When disabled, always stick to each version's default database (no switching).
        if (!options.Value.Enabled)
            return GetDefaultDatabase(versionPath);

        var defaultDb = GetDefaultDatabase(versionPath);
        var otherDb = defaultDb == DatabaseType.MySql ? DatabaseType.MongoDb : DatabaseType.MySql;

        var defaultHealthy = IsHealthy(defaultDb);
        var otherHealthy = IsHealthy(otherDb);

        DatabaseType active;
        bool fallbackActive;

        if (defaultHealthy)
        {
            active = defaultDb;
            fallbackActive = false;
        }
        else if (otherHealthy)
        {
            active = otherDb;
            fallbackActive = true;
        }
        else
        {
            active = DatabaseType.BothDown;
            fallbackActive = false;
        }

        LogTransitions(versionPath, active, fallbackActive);
        return active;
    }

    private void LogTransitions(string versionPath, DatabaseType active, bool fallbackActive)
    {
        lock (_lock)
        {
            var key = NormalizeVersionPath(versionPath);
            if (!_last.TryGetValue(key, out var last))
            {
                _last[key] = (active, fallbackActive);
                if (active == DatabaseType.BothDown)
                    logger.LogWarning("Both databases are down for {VersionPath}.", key);
                else if (fallbackActive)
                    logger.LogWarning("Database fallback activated for {VersionPath}. Active database: {Database}.", key, active);
                return;
            }

            if (last.active == active && last.fallbackActive == fallbackActive)
                return;

            _last[key] = (active, fallbackActive);

            if (active == DatabaseType.BothDown)
            {
                logger.LogWarning("Both databases are down for {VersionPath}.", key);
            }
            else if (fallbackActive)
            {
                logger.LogWarning("Database fallback activated for {VersionPath}. Active database: {Database}.", key, active);
            }
            else
            {
                logger.LogWarning("Database fallback deactivated for {VersionPath}. Active database: {Database}.", key, active);
            }
        }
    }

    private bool IsHealthy(DatabaseType db) =>
        db switch
        {
            DatabaseType.MySql => health.IsMySqlHealthy,
            DatabaseType.MongoDb => health.IsMongoDbHealthy,
            _ => false
        };

    private static DatabaseType GetDefaultDatabase(string versionPath)
    {
        var v = NormalizeVersionPath(versionPath);
        // Phase 2 contract: v1 defaults to MySQL, v2 defaults to MongoDB.
        return v.StartsWith("/api/v2/", StringComparison.OrdinalIgnoreCase)
            ? DatabaseType.MongoDb
            : DatabaseType.MySql;
    }

    private static string NormalizeVersionPath(string versionPath)
    {
        if (string.IsNullOrWhiteSpace(versionPath))
            return "/api/v1/";

        var p = versionPath.Trim();
        if (p.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase)) return "/api/v1/";
        if (p.StartsWith("/api/v2/", StringComparison.OrdinalIgnoreCase)) return "/api/v2/";
        if (p.Contains("/api/v1/", StringComparison.OrdinalIgnoreCase)) return "/api/v1/";
        if (p.Contains("/api/v2/", StringComparison.OrdinalIgnoreCase)) return "/api/v2/";
        return "/api/v1/";
    }
}

