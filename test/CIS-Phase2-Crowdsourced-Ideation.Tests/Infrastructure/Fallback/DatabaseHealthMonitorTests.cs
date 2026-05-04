using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Fallback;

public sealed class DatabaseHealthMonitorTests
{
    private readonly HealthStatusCache _cache = new();
    private readonly Mock<IMySqlHealthCheck> _mySqlHealthCheckMock = new();
    private readonly Mock<IMongoDbHealthCheck> _mongoDbHealthCheckMock = new();
    private readonly Mock<ILogger<DatabaseHealthMonitor>> _loggerMock = new();
    private readonly IOptions<FallbackOptions> _options;

    public DatabaseHealthMonitorTests()
    {
        _options = Options.Create(new FallbackOptions { HealthCheckIntervalSeconds = 1 });
    }

    [Fact]
    public async Task ExecuteAsync_ProbesHealthAndUpdatesCache()
    {
        _mySqlHealthCheckMock.Setup(h => h.CheckHealthAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HealthCheckResult.Healthy());
        _mongoDbHealthCheckMock.Setup(h => h.CheckHealthAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HealthCheckResult.Healthy());

        var sut = new DatabaseHealthMonitor(
            _cache,
            _mySqlHealthCheckMock.Object,
            _mongoDbHealthCheckMock.Object,
            _options,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();
        var task = sut.StartAsync(cts.Token);
        
        await Task.Delay(100);
        await sut.StopAsync(cts.Token);

        _cache.IsMySqlHealthy.Should().BeTrue();
        _cache.IsMongoDbHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task ProbeMySqlAsync_WhenThrows_MarksAsUnhealthy()
    {
        _mySqlHealthCheckMock.Setup(h => h.CheckHealthAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Fail"));
        _mongoDbHealthCheckMock.Setup(h => h.CheckHealthAsync(It.IsAny<HealthCheckContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HealthCheckResult.Healthy());

        var sut = new DatabaseHealthMonitor(
            _cache,
            _mySqlHealthCheckMock.Object,
            _mongoDbHealthCheckMock.Object,
            _options,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await sut.StopAsync(cts.Token);

        _cache.IsMySqlHealthy.Should().BeFalse();
    }
}
