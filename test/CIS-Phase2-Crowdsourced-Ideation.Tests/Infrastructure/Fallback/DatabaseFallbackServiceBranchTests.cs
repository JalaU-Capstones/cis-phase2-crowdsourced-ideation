using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Fallback;

public sealed class DatabaseFallbackServiceBranchTests
{
    [Theory]
    [InlineData(null, "/api/v1/")]
    [InlineData("", "/api/v1/")]
    [InlineData("   ", "/api/v1/")]
    [InlineData("/api/v1", "/api/v1/")]
    [InlineData("/api/v2", "/api/v2/")]
    [InlineData("/api/v1/topics", "/api/v1/")]
    [InlineData("/x/api/v2/topics", "/api/v2/")]
    public void NormalizePath_IsCoveredThroughPublicApi(string? input, string expectedNormalized)
    {
        var health = new HealthStatusCache();
        health.TrySetMySqlHealthy(true, out _);
        health.TrySetMongoHealthy(true, out _);

        var sut = new DatabaseFallbackService(health, Options.Create(new FallbackOptions { Enabled = true }), NullLogger<DatabaseFallbackService>.Instance);
        sut.GetActiveDatabase(input ?? string.Empty).Should().Be(expectedNormalized == "/api/v2/" ? DatabaseType.MongoDb : DatabaseType.MySql);
    }

    [Fact]
    public void GetActiveDatabase_WhenDisabled_ReturnsDefault()
    {
        var health = new HealthStatusCache();
        health.TrySetMySqlHealthy(false, out _);
        health.TrySetMongoHealthy(false, out _);

        var sut = new DatabaseFallbackService(health, Options.Create(new FallbackOptions { Enabled = false }), NullLogger<DatabaseFallbackService>.Instance);
        sut.GetActiveDatabase("/api/v2/topics").Should().Be(DatabaseType.MongoDb);
        sut.IsFallbackActiveForVersion("/api/v2/topics").Should().BeFalse();
    }

    [Fact]
    public void GetActiveDatabase_DefaultUnhealthyOtherHealthy_FallsBack()
    {
        var health = new HealthStatusCache();
        health.TrySetMySqlHealthy(false, out _);
        health.TrySetMongoHealthy(true, out _);

        var sut = new DatabaseFallbackService(health, Options.Create(new FallbackOptions { Enabled = true }), NullLogger<DatabaseFallbackService>.Instance);
        sut.GetActiveDatabase("/api/v1/topics").Should().Be(DatabaseType.MongoDb);
        sut.IsFallbackActiveForVersion("/api/v1/topics").Should().BeTrue();
    }

    [Fact]
    public void GetActiveDatabase_BothUnhealthy_ReturnsBothDown_AndFallbackInactive()
    {
        var health = new HealthStatusCache();
        health.TrySetMySqlHealthy(false, out _);
        health.TrySetMongoHealthy(false, out _);

        var sut = new DatabaseFallbackService(health, Options.Create(new FallbackOptions { Enabled = true }), NullLogger<DatabaseFallbackService>.Instance);
        sut.GetActiveDatabase("/api/v1/topics").Should().Be(DatabaseType.BothDown);
        sut.IsFallbackActiveForVersion("/api/v1/topics").Should().BeFalse();
    }
}

