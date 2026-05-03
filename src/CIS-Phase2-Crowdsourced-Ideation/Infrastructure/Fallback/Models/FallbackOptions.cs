namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;

/// <summary>
/// Configuration options for the emergency database fallback mechanism.
/// </summary>
public sealed class FallbackOptions
{
    /// <summary>
    /// When false, the fallback mechanism is bypassed (no switching and no maintenance/outage responses).
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The interval (in seconds) at which database health is re-evaluated.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; init; } = 10;
}

