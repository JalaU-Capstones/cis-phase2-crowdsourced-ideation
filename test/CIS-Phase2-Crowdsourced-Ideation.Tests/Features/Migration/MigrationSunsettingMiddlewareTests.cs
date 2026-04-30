using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Migration;

public sealed class MigrationSunsettingMiddlewareTests
{

    private bool _nextWasCalled;

    private MigrationSunsettingMiddleware Build(MigrationStateManager state)
    {
        _nextWasCalled = false;
        RequestDelegate next = _ => { _nextWasCalled = true; return Task.CompletedTask; };
        return new MigrationSunsettingMiddleware(
            next,
            state,
            NullLogger<MigrationSunsettingMiddleware>.Instance);
    }

    private static HttpContext MakeContext(string method, string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path   = path;
        ctx.Response.Body  = new MemoryStream();
        return ctx;
    }

    private static async Task<string> ReadBody(HttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    }
    

    [Theory]
    [InlineData("GET",    "/api/v1/topics")]
    [InlineData("POST",   "/api/v1/topics")]
    [InlineData("PUT",    "/api/v1/topics/1")]
    [InlineData("DELETE", "/api/v1/topics/1")]
    [InlineData("GET",    "/api/v2/topics")]
    [InlineData("POST",   "/api/v2/topics")]
    [InlineData("DELETE", "/api/v2/votes/1")]
    public async Task Phase1_NormalState_AllMethodsPassThrough(string method, string path)
    {
        var sut = Build(new MigrationStateManager());
        await sut.InvokeAsync(MakeContext(method, path));
        _nextWasCalled.Should().BeTrue();
    }
    

    [Theory]
    [InlineData("POST",   "/api/v1/topics")]
    [InlineData("PUT",    "/api/v1/topics/1")]
    [InlineData("DELETE", "/api/v1/topics/1")]
    [InlineData("POST",   "/api/v2/ideas")]
    [InlineData("PUT",    "/api/v2/votes/1")]
    [InlineData("DELETE", "/api/v2/votes/1")]
    public async Task Phase2_MigrationRunning_WriteOps_Return503(string method, string path)
    {
        var state = new MigrationStateManager();
        state.SetMigrationRunning(true);
        var ctx = MakeContext(method, path);

        await Build(state).InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        _nextWasCalled.Should().BeFalse();
        (await ReadBody(ctx)).Should().Contain("maintenance");
    }

    [Theory]
    [InlineData("/api/v1/topics")]
    [InlineData("/api/v2/ideas")]
    [InlineData("/swagger")]
    public async Task Phase2_MigrationRunning_GetRequests_PassThrough(string path)
    {
        var state = new MigrationStateManager();
        state.SetMigrationRunning(true);

        await Build(state).InvokeAsync(MakeContext("GET", path));

        _nextWasCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("POST",   "/api/v1/topics")]
    [InlineData("PUT",    "/api/v1/topics/1")]
    [InlineData("DELETE", "/api/v1/votes/1")]
    [InlineData("POST",   "/api/v1/ideas")]
    public async Task Phase3_HasMigrated_V1_WriteOps_Return410(string method, string path)
    {
        var state = new MigrationStateManager();
        state.SetHasMigrated(true);
        var ctx = MakeContext(method, path);

        await Build(state).InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status410Gone);
        _nextWasCalled.Should().BeFalse();
        (await ReadBody(ctx)).Should().Contain("/api/v2/");
    }

    [Theory]
    [InlineData("/api/v1/topics")]
    [InlineData("/api/v1/ideas")]
    [InlineData("/api/v1/votes")]
    [InlineData("/api/v1")]
    public async Task Phase3_HasMigrated_V1_GetRequests_PassThroughWithWarningHeader(string path)
    {
        var state = new MigrationStateManager();
        state.SetHasMigrated(true);
        var ctx = MakeContext("GET", path);

        await Build(state).InvokeAsync(ctx);

        _nextWasCalled.Should().BeTrue();
        ctx.Response.Headers["Warning"].ToString()
            .Should().Contain("299").And.Contain("/api/v2/");
    }

    [Theory]
    [InlineData("GET",    "/api/v2/topics")]
    [InlineData("POST",   "/api/v2/topics")]
    [InlineData("DELETE", "/api/v2/votes/1")]
    public async Task Phase3_HasMigrated_V2_AllRequests_PassThroughNoWarning(string method, string path)
    {
        var state = new MigrationStateManager();
        state.SetHasMigrated(true);
        var ctx = MakeContext(method, path);

        await Build(state).InvokeAsync(ctx);

        _nextWasCalled.Should().BeTrue();
        ctx.Response.Headers.ContainsKey("Warning").Should().BeFalse();
    }
    

    [Fact]
    public async Task BothFlagsTrue_WriteOnV1_503TakesPrecedenceOver410()
    {
        
        var state = new MigrationStateManager();
        state.SetMigrationRunning(true);
        state.SetHasMigrated(true);
        var ctx = MakeContext("POST", "/api/v1/topics");

        await Build(state).InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Theory]
    [InlineData("POST",   "/swagger")]
    [InlineData("DELETE", "/health")]
    [InlineData("PUT",    "/metrics")]
    public async Task NonApiPaths_NeverBlocked_EvenWhenBothFlagsTrue(string method, string path)
    {
        var state = new MigrationStateManager();
        state.SetMigrationRunning(true);
        state.SetHasMigrated(true);

        await Build(state).InvokeAsync(MakeContext(method, path));

        _nextWasCalled.Should().BeTrue();
    }
}