using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Fallback;

public sealed class DatabaseFallbackServiceTests
{
    [Theory]
    [InlineData("/api/v1/topics", true, true, DatabaseType.MySql, false)]
    [InlineData("/api/v1/topics", false, true, DatabaseType.MongoDb, true)]
    [InlineData("/api/v1/topics", false, false, DatabaseType.BothDown, false)]
    [InlineData("/api/v2/topics", true, true, DatabaseType.MongoDb, false)]
    [InlineData("/api/v2/topics", true, false, DatabaseType.MySql, true)]
    [InlineData("/api/v2/topics", false, false, DatabaseType.BothDown, false)]
    public void GetActiveDatabase_UsesDefaultsAndFallbackRules(
        string path,
        bool mySqlHealthy,
        bool mongoHealthy,
        DatabaseType expectedDb,
        bool expectedFallbackActive)
    {
        var cache = new HealthStatusCache();
        cache.TrySetMySqlHealthy(mySqlHealthy, out _);
        cache.TrySetMongoHealthy(mongoHealthy, out _);

        var sut = new DatabaseFallbackService(
            cache,
            Options.Create(new FallbackOptions { Enabled = true, HealthCheckIntervalSeconds = 10 }),
            NullLogger<DatabaseFallbackService>.Instance);

        var active = sut.GetActiveDatabase(path);
        active.Should().Be(expectedDb);

        sut.IsFallbackActiveForVersion(path).Should().Be(expectedFallbackActive);
    }

    [Fact]
    public void WhenDisabled_DoesNotSwitchAndNeverReportsFallback()
    {
        var cache = new HealthStatusCache();
        cache.TrySetMySqlHealthy(false, out _);
        cache.TrySetMongoHealthy(true, out _);

        var sut = new DatabaseFallbackService(
            cache,
            Options.Create(new FallbackOptions { Enabled = false }),
            NullLogger<DatabaseFallbackService>.Instance);

        sut.GetActiveDatabase("/api/v1/topics").Should().Be(DatabaseType.MySql);
        sut.GetActiveDatabase("/api/v2/topics").Should().Be(DatabaseType.MongoDb);
        sut.IsFallbackActiveForVersion("/api/v1/topics").Should().BeFalse();
        sut.IsFallbackActiveForVersion("/api/v2/topics").Should().BeFalse();
    }
}

