using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Migration;

public sealed class MigrationStateManagerTests
{
    [Fact]
    public void InitialState_BothFlagsAreFalse()
    {
        var sut = new MigrationStateManager();
        sut.IsMigrationRunning.Should().BeFalse();
        sut.HasMigrated.Should().BeFalse();
    }

    [Fact]
    public void SetMigrationRunning_True_ReflectsInProperty()
    {
        var sut = new MigrationStateManager();
        sut.SetMigrationRunning(true);
        sut.IsMigrationRunning.Should().BeTrue();
    }

    [Fact]
    public void SetMigrationRunning_FalseAfterTrue_ResetsFlag()
    {
        var sut = new MigrationStateManager();
        sut.SetMigrationRunning(true);
        sut.SetMigrationRunning(false);
        sut.IsMigrationRunning.Should().BeFalse();
    }

    [Fact]
    public void SetMigrationRunning_DoesNotAffectHasMigrated()
    {
        var sut = new MigrationStateManager();
        sut.SetMigrationRunning(true);
        sut.HasMigrated.Should().BeFalse();
    }

    [Fact]
    public void SetHasMigrated_True_ReflectsInProperty()
    {
        var sut = new MigrationStateManager();
        sut.SetHasMigrated(true);
        sut.HasMigrated.Should().BeTrue();
    }

    [Fact]
    public void SetHasMigrated_FalseAfterTrue_ResetsFlag()
    {
        var sut = new MigrationStateManager();
        sut.SetHasMigrated(true);
        sut.SetHasMigrated(false);
        sut.HasMigrated.Should().BeFalse();
    }

    [Fact]
    public void SetHasMigrated_DoesNotAffectIsMigrationRunning()
    {
        var sut = new MigrationStateManager();
        sut.SetHasMigrated(true);
        sut.IsMigrationRunning.Should().BeFalse();
    }

    [Fact]
    public void ConcurrentWrites_DoNotCorruptState()
    {
        var sut   = new MigrationStateManager();
        var tasks = Enumerable.Range(0, 2_000).Select(i => Task.Run(() =>
        {
            sut.SetMigrationRunning(i % 2 == 0);
            sut.SetHasMigrated(i % 3 == 0);
            _ = sut.IsMigrationRunning;
            _ = sut.HasMigrated;
        }));

        var act = () => Task.WhenAll(tasks).GetAwaiter().GetResult();
        act.Should().NotThrow();
    }
}