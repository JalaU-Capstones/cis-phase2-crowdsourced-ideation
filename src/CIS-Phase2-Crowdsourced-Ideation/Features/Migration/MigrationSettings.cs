// src/CIS-Phase2-Crowdsourced-Ideation/Features/Migration/MigrationSettings.cs
namespace CIS.Phase2.CrowdsourcedIdeation.Features.Migration;

/// <summary>
/// Bound from the "MigrationSettings" section of appsettings.json.
/// Can be overridden per-run via CLI:
///   --MigrationSettings:RunOnStartup=true --MigrationSettings:DowntimeSeconds=30
/// </summary>
public sealed class MigrationSettings
{
    public const string SectionName = "MigrationSettings";

    /// <summary>Base URL of the Java Phase 1 API (e.g. http://localhost:8080).</summary>
    public string Phase1BaseUrl { get; init; } = "http://localhost:8080";

    /// <summary>When true, the worker runs the full ELT pipeline on startup.</summary>
    public bool RunOnStartup { get; init; } = false;

    /// <summary>
    /// Seconds to wait in maintenance mode after the C# migration completes
    /// and before sending the sunset signal to Java Phase 1.
    /// </summary>
    public int DowntimeSeconds { get; init; } = 30;
}