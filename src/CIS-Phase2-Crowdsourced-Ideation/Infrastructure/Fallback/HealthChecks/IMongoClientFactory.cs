using MongoDB.Driver;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

/// <summary>
/// Factory abstraction for creating MongoDB clients (to make health checks testable).
/// </summary>
public interface IMongoClientFactory
{
    /// <summary>Creates a MongoDB client.</summary>
    IMongoClient Create();
}

