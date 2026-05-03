using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback;

/// <summary>
/// Determines which database should be used for a given request and whether fallback is active.
/// </summary>
public interface IDatabaseFallbackService
{
    /// <summary>
    /// Returns true when the API is serving the given version from the non-default database.
    /// </summary>
    bool IsFallbackActiveForVersion(string versionPath);

    /// <summary>
    /// Gets the active database for the given version path (e.g. <c>/api/v1/</c> or <c>/api/v2/</c>).
    /// </summary>
    DatabaseType GetActiveDatabase(string versionPath);
}

