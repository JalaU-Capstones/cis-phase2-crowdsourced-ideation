using System.Diagnostics.CodeAnalysis;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

/// <summary>
/// Thread-safe cache for database health statuses. Updated periodically by a background service.
/// </summary>
public sealed class HealthStatusCache
{
    private volatile bool _mySqlHealthy = true;
    private volatile bool _mongoHealthy = true;

    /// <summary>True when MySQL connectivity probes succeed.</summary>
    public bool IsMySqlHealthy => _mySqlHealthy;

    /// <summary>True when MongoDB connectivity probes succeed.</summary>
    public bool IsMongoDbHealthy => _mongoHealthy;

    /// <summary>Sets the cached health flag for MySQL.</summary>
    public bool TrySetMySqlHealthy(bool healthy, [NotNullWhen(true)] out (bool from, bool to)? transition)
    {
        var from = _mySqlHealthy;
        _mySqlHealthy = healthy;
        if (from == healthy)
        {
            transition = null;
            return false;
        }

        transition = (from, healthy);
        return true;
    }

    /// <summary>Sets the cached health flag for MongoDB.</summary>
    public bool TrySetMongoHealthy(bool healthy, [NotNullWhen(true)] out (bool from, bool to)? transition)
    {
        var from = _mongoHealthy;
        _mongoHealthy = healthy;
        if (from == healthy)
        {
            transition = null;
            return false;
        }

        transition = (from, healthy);
        return true;
    }
}

