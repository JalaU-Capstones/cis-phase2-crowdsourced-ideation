using CIS.Phase2.CrowdsourcedIdeation.Features.Migration;
using FluentAssertions;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Migration;

public sealed class MigrationSettingsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var settings = new MigrationSettings();
        settings.RunOnStartup.Should().BeFalse();
        settings.DowntimeSeconds.Should().Be(30);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var settings = new MigrationSettings
        {
            RunOnStartup = true,
            DowntimeSeconds = 5
        };
        settings.RunOnStartup.Should().BeTrue();
        settings.DowntimeSeconds.Should().Be(5);
    }
}
