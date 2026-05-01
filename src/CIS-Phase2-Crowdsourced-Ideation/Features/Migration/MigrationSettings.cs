namespace CIS.Phase2.CrowdsourcedIdeation.Features.Migration;

public sealed class MigrationSettings
{
    public const string SectionName = "MigrationSettings";

    public bool RunOnStartup { get; init; } = false;

    public int DowntimeSeconds { get; init; } = 30;
}