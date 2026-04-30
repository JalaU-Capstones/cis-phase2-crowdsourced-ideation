using System.Net;
using CIS.Phase2.CrowdsourcedIdeation.Features.Migration;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Migration;

public sealed class AutomatedMigrationWorkerTests
{
    private static IHttpClientFactory HttpFactory(params HttpStatusCode[] codes)
    {
        var queue   = new Queue<HttpStatusCode>(codes);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(
                queue.Count > 0 ? queue.Dequeue() : HttpStatusCode.OK));

        var client  = new HttpClient(handler.Object) { BaseAddress = new Uri("http://java-phase1") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(AutomatedMigrationWorker.HttpClientName)).Returns(client);
        return factory.Object;
    }

    private static IServiceScopeFactory ScopeFactory(IMigrationService migSvc)
    {
        var sp = new ServiceCollection()
            .AddSingleton(migSvc)
            .BuildServiceProvider();

        return sp.GetRequiredService<IServiceScopeFactory>();
    }

    private static MigrationResult GoodResult(long t = 5, long i = 20, long v = 50)
        => new(t, i, v, new ValidationResult(
            new CountPair(t, t), new CountPair(i, i), new CountPair(v, v)));

    private static MigrationResult BadResult()
        => new(5, 20, 50, new ValidationResult(
            new CountPair(5, 5),
            new CountPair(20, 18),
            new CountPair(50, 50)));

    private static AutomatedMigrationWorker Build(
        MigrationStateManager state,
        IHttpClientFactory    factory,
        IMigrationService     svc,
        bool                  runOnStartup    = true,
        int                   downtimeSeconds = 0)
    {
        var settings = Options.Create(new MigrationSettings
        {
            Phase1BaseUrl   = "http://java-phase1",
            RunOnStartup    = runOnStartup,
            DowntimeSeconds = downtimeSeconds
        });
        return new AutomatedMigrationWorker(
            settings,
            state,
            factory,
            ScopeFactory(svc),
            NullLogger<AutomatedMigrationWorker>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_RunOnStartupFalse_NothingRuns()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();
        var worker = Build(state, HttpFactory(), migSvc.Object, runOnStartup: false);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        migSvc.Verify(m => m.RunAsync(), Times.Never);
        state.IsMigrationRunning.Should().BeFalse();
        state.HasMigrated.Should().BeFalse();
    }

    [Fact]
    public async Task RunMigrationAsync_HappyPath_SetsHasMigratedAndClearsRunning()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();
        migSvc.Setup(m => m.RunAsync()).ReturnsAsync(GoodResult());

        var worker = Build(state,
            HttpFactory(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            migSvc.Object);
        await worker.RunMigrationAsync(CancellationToken.None);

        state.HasMigrated.Should().BeTrue();
        state.IsMigrationRunning.Should().BeFalse();
        migSvc.Verify(m => m.RunAsync(), Times.Once);
    }

    [Fact]
    public async Task RunMigrationAsync_JavaMaintenanceStartFails_AbortsAndResetsFlags()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();

        var worker = Build(state, HttpFactory(HttpStatusCode.InternalServerError), migSvc.Object);
        await worker.RunMigrationAsync(CancellationToken.None);

        state.HasMigrated.Should().BeFalse();
        state.IsMigrationRunning.Should().BeFalse();
        migSvc.Verify(m => m.RunAsync(), Times.Never);
    }

    [Fact]
    public async Task RunMigrationAsync_JavaMigrateFails_NoCSharpMigrationAndFlagsReset()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();

        var worker = Build(state, HttpFactory(HttpStatusCode.OK, HttpStatusCode.InternalServerError), migSvc.Object);
        await worker.RunMigrationAsync(CancellationToken.None);

        state.HasMigrated.Should().BeFalse();
        state.IsMigrationRunning.Should().BeFalse();
        migSvc.Verify(m => m.RunAsync(), Times.Never);
    }

    [Fact]
    public async Task RunMigrationAsync_MigrationServiceThrows_FlagsReset()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();
        migSvc.Setup(m => m.RunAsync())
              .ThrowsAsync(new InvalidOperationException("Missing users"));

        var worker = Build(state, HttpFactory(HttpStatusCode.OK, HttpStatusCode.OK), migSvc.Object);
        await worker.RunMigrationAsync(CancellationToken.None);

        state.HasMigrated.Should().BeFalse();
        state.IsMigrationRunning.Should().BeFalse();
    }

    [Fact]
    public async Task RunMigrationAsync_InconsistentResult_NoSunsetAndHasMigratedFalse()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();
        migSvc.Setup(m => m.RunAsync()).ReturnsAsync(BadResult());

        int callCount = 0;
        var handler   = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => { callCount++; return new HttpResponseMessage(HttpStatusCode.OK); });

        var client  = new HttpClient(handler.Object) { BaseAddress = new Uri("http://java") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(AutomatedMigrationWorker.HttpClientName)).Returns(client);

        var worker = Build(state, factory.Object, migSvc.Object);
        await worker.RunMigrationAsync(CancellationToken.None);

        state.HasMigrated.Should().BeFalse();
        callCount.Should().Be(2, "only maintenance/start and migrate are called; stop and sunset never reached");
    }

    [Fact]
    public async Task RunMigrationAsync_JavaSunsetFails_HasMigratedStaysFalse()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();
        migSvc.Setup(m => m.RunAsync()).ReturnsAsync(GoodResult());

        var worker = Build(state,
            HttpFactory(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError),
            migSvc.Object);
        await worker.RunMigrationAsync(CancellationToken.None);

        state.HasMigrated.Should().BeFalse();
        state.IsMigrationRunning.Should().BeFalse();
    }

    [Fact]
    public async Task RunMigrationAsync_ZeroDowntime_CompletesQuickly()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();
        migSvc.Setup(m => m.RunAsync()).ReturnsAsync(GoodResult());

        var worker = Build(state,
            HttpFactory(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            migSvc.Object,
            downtimeSeconds: 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await worker.RunMigrationAsync(CancellationToken.None);
        sw.Stop();

        state.HasMigrated.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(2_000);
    }

    [Fact]
    public async Task RunMigrationAsync_CancelledDuringDowntime_FlagsReset()
    {
        var state  = new MigrationStateManager();
        var migSvc = new Mock<IMigrationService>();
        migSvc.Setup(m => m.RunAsync()).ReturnsAsync(GoodResult());

        using var cts = new CancellationTokenSource(millisecondsDelay: 150);

        var worker = Build(state,
            HttpFactory(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            migSvc.Object,
            downtimeSeconds: 60);

        await worker.RunMigrationAsync(cts.Token);

        state.HasMigrated.Should().BeFalse("sunset was never reached");
        state.IsMigrationRunning.Should().BeFalse("finally always runs");
    }
}