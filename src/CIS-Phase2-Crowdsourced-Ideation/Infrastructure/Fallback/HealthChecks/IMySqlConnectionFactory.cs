using System.Data.Common;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

/// <summary>
/// Factory abstraction for creating MySQL connections (to make health checks testable).
/// </summary>
public interface IMySqlConnectionFactory
{
    /// <summary>Creates a new <see cref="DbConnection"/> instance.</summary>
    DbConnection Create();
}

