using System.Net;
using System.Text.Json;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Middleware;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using System.IO;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Fallback;

public sealed class DatabaseFallbackMiddlewareTests
{
    [Fact]
    public async Task WhenBothDown_Returns503ForAnyMethod()
    {
        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.BothDown);

        var called = false;
        var middleware = new DatabaseFallbackMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            fallback.Object,
            Options.Create(new FallbackOptions { Enabled = true }),
            NullLogger<DatabaseFallbackMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Path = "/api/v2/topics";

        await middleware.InvokeAsync(ctx);

        called.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);

        ctx.Response.Body.Position = 0;
        var doc = await JsonDocument.ParseAsync(ctx.Response.Body);
        doc.RootElement.GetProperty("error").GetString()
            .Should().Be("Please try again later. Our maintenance team is working to resolve this issue.");
    }

    [Fact]
    public async Task WhenFallbackActiveAndWrite_Returns503MaintenanceMessage()
    {
        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.MongoDb);
        fallback.Setup(f => f.IsFallbackActiveForVersion(It.IsAny<string>())).Returns(true);

        var called = false;
        var middleware = new DatabaseFallbackMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            fallback.Object,
            Options.Create(new FallbackOptions { Enabled = true }),
            NullLogger<DatabaseFallbackMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Request.Path = "/api/v1/topics";

        await middleware.InvokeAsync(ctx);

        called.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);

        ctx.Response.Body.Position = 0;
        var doc = await JsonDocument.ParseAsync(ctx.Response.Body);
        doc.RootElement.GetProperty("error").GetString()
            .Should().StartWith("Our system is currently undergoing planned maintenance.");
    }

    [Fact]
    public async Task WhenHealthyOrGet_AllowsPipeline()
    {
        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.MongoDb);
        fallback.Setup(f => f.IsFallbackActiveForVersion(It.IsAny<string>())).Returns(false);

        var called = false;
        var middleware = new DatabaseFallbackMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            fallback.Object,
            Options.Create(new FallbackOptions { Enabled = true }),
            NullLogger<DatabaseFallbackMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Path = "/api/v2/topics";

        await middleware.InvokeAsync(ctx);
        called.Should().BeTrue();
    }

    [Fact]
    public async Task WhenDisabled_AlwaysAllowsPipeline()
    {
        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.BothDown);

        var called = false;
        var middleware = new DatabaseFallbackMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            fallback.Object,
            Options.Create(new FallbackOptions { Enabled = false }),
            NullLogger<DatabaseFallbackMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Request.Path = "/api/v1/topics";

        await middleware.InvokeAsync(ctx);
        called.Should().BeTrue();
    }
}
