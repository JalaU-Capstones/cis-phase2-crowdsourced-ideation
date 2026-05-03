namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;

/// <summary>
/// Represents the database that should be used to serve the current request.
/// </summary>
public enum DatabaseType
{
    /// <summary>Use MySQL (Phase 2 V1 default).</summary>
    MySql,

    /// <summary>Use MongoDB (Phase 2 V2 default).</summary>
    MongoDb,

    /// <summary>Neither MySQL nor MongoDB is available.</summary>
    BothDown
}

