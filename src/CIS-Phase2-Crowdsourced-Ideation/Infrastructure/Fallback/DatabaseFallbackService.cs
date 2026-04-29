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
        var normalizedPath = NormalizeVersionPath(versionPath);
        var defaultDb = GetDefaultDatabase(normalizedPath);

        // When disabled, always stick to each version's default database (no switching).
        if (!options.Value.Enabled)
        {
            LogDecision(normalizedPath, defaultDb, fallbackActive: false, defaultDb, defaultHealthy: true, otherHealthy: true);
            return defaultDb;
        }

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

        LogDecision(normalizedPath, active, fallbackActive, defaultDb, defaultHealthy, otherHealthy);
        return active;
    }

    private void LogDecision(
        string versionPath,
        DatabaseType active,
        bool fallbackActive,
        DatabaseType defaultDb,
        bool defaultHealthy,
        bool otherHealthy)
    {
        lock (_lock)
        {
            logger.LogWarning(
                "Fallback decision for {VersionPath}: default={DefaultDatabase}, defaultHealthy={DefaultHealthy}, otherHealthy={OtherHealthy}, active={ActiveDatabase}, fallbackActive={FallbackActive}.",
                versionPath,
                defaultDb,
                defaultHealthy,
                otherHealthy,
                active,
                fallbackActive);
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
        // Phase 2 contract: v1 defaults to MySQL, v2 defaults to MongoDB.
        return versionPath.StartsWith("/api/v2/", StringComparison.OrdinalIgnoreCase)
            ? DatabaseType.MongoDb
            : DatabaseType.MySql;
    }

    private static string NormalizeVersionPath(string versionPath)
    {
        if (string.IsNullOrWhiteSpace(versionPath))
            return "/api/v1/";

        var p = versionPath.Trim();
        if (p.Equals("/api/v1", StringComparison.OrdinalIgnoreCase)) return "/api/v1/";
        if (p.Equals("/api/v2", StringComparison.OrdinalIgnoreCase)) return "/api/v2/";
        if (p.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase)) return "/api/v1/";
        if (p.StartsWith("/api/v2/", StringComparison.OrdinalIgnoreCase)) return "/api/v2/";
        if (p.Contains("/api/v1/", StringComparison.OrdinalIgnoreCase)) return "/api/v1/";
        if (p.Contains("/api/v2/", StringComparison.OrdinalIgnoreCase)) return "/api/v2/";
        return "/api/v1/";
    }
}

