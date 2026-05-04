using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Routing;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Fallback;

public sealed class FallbackAdapterTests
{
    [Fact]
    public async Task DelegatesToMySql_WhenFallbackSaysMySql()
    {
        var myTopics = new Mock<ITopicRepository>().Object;
        var mySql = new StubAdapter(myTopics);
        var mongo = new StubAdapter(new Mock<ITopicRepository>().Object);

        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.MySql);

        var http = new DefaultHttpContext();
        http.Request.Path = "/api/v2/topics";
        var accessor = new HttpContextAccessor { HttpContext = http };
        using var services = BuildServices(mySql, mongo);

        var sut = new FallbackAdapter(services, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

        sut.Topics.Should().BeSameAs(myTopics);
        await sut.SaveChangesAsync();
        mySql.SaveChangesCalls.Should().Be(1);
        mongo.SaveChangesCalls.Should().Be(0);
    }

    [Fact]
    public async Task DelegatesToMongo_WhenFallbackSaysMongo()
    {
        var mongoTopics = new Mock<ITopicRepository>().Object;
        var mySql = new StubAdapter(new Mock<ITopicRepository>().Object);
        var mongo = new StubAdapter(mongoTopics);

        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.MongoDb);

        var http = new DefaultHttpContext();
        http.Request.Path = "/api/v1/topics";
        var accessor = new HttpContextAccessor { HttpContext = http };
        using var services = BuildServices(mySql, mongo);

        var sut = new FallbackAdapter(services, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

        sut.Topics.Should().BeSameAs(mongoTopics);
        await sut.SaveChangesAsync();
        mongo.SaveChangesCalls.Should().Be(1);
        mySql.SaveChangesCalls.Should().Be(0);
    }

    [Fact]
    public async Task ReEvaluatesPerCall_AndUsesCurrentRequestPath()
    {
        var myTopics = new Mock<ITopicRepository>().Object;
        var mongoTopics = new Mock<ITopicRepository>().Object;
        var mySql = new StubAdapter(myTopics);
        var mongo = new StubAdapter(mongoTopics);

        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>()))
            .Returns<string>(path => path.StartsWith("/api/v2/", StringComparison.OrdinalIgnoreCase)
                ? DatabaseType.MongoDb
                : DatabaseType.MySql);

        var http = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = http };
        using var services = BuildServices(mySql, mongo);
        var sut = new FallbackAdapter(services, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

        http.Request.Path = "/api/v1/topics";
        sut.Topics.Should().BeSameAs(myTopics);
        await sut.SaveChangesAsync();

        http.Request.Path = "/api/v2/topics";
        sut.Topics.Should().BeSameAs(mongoTopics);
        await sut.SaveChangesAsync();

        mySql.SaveChangesCalls.Should().Be(1);
        mongo.SaveChangesCalls.Should().Be(1);
        fallback.Verify(f => f.GetActiveDatabase("/api/v1/topics"), Times.AtLeastOnce);
        fallback.Verify(f => f.GetActiveDatabase("/api/v2/topics"), Times.AtLeastOnce);
    }

    [Fact]
    public void BothDown_ThrowsInvalidOperation()
    {
        var mySql = new StubAdapter(new Mock<ITopicRepository>().Object);
        var mongo = new StubAdapter(new Mock<ITopicRepository>().Object);
        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.BothDown);

        var http = new DefaultHttpContext();
        http.Request.Path = "/api/v1/topics";
        var accessor = new HttpContextAccessor { HttpContext = http };
        using var services = BuildServices(mySql, mongo);
        var sut = new FallbackAdapter(services, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

        var act = () => _ = sut.Topics;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Both databases are down.");
    }

    [Fact]
    public void UnsupportedDatabaseType_ThrowsInvalidOperation()
    {
        var mySql = new StubAdapter(new Mock<ITopicRepository>().Object);
        var mongo = new StubAdapter(new Mock<ITopicRepository>().Object);
        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns((DatabaseType)999);

        var http = new DefaultHttpContext();
        http.Request.Path = "/api/v1/topics";
        var accessor = new HttpContextAccessor { HttpContext = http };
        using var services = BuildServices(mySql, mongo);
        var sut = new FallbackAdapter(services, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

        var act = () => _ = sut.Topics;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported database type*");
    }

    [Fact]
    public async Task SaveChangesAsync_DelegatesToActive()
    {
        var mySql = new StubAdapter(new Mock<ITopicRepository>().Object);
        var mongo = new StubAdapter(new Mock<ITopicRepository>().Object);
        var fallback = new Mock<CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.IDatabaseFallbackService>();
        fallback.Setup(f => f.GetActiveDatabase(It.IsAny<string>())).Returns(DatabaseType.MySql);

        var http = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = http };
        using var services = BuildServices(mySql, mongo);
        var sut = new FallbackAdapter(services, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

        await sut.SaveChangesAsync();
        mySql.SaveChangesCalls.Should().Be(1);
    }

    private sealed class StubAdapter(ITopicRepository topics) : IRepositoryAdapter
    {
        public int SaveChangesCalls { get; private set; }

        public ITopicRepository Topics => topics;
        public IIdeaRepository Ideas => Mock.Of<IIdeaRepository>();
        public IVoteRepository Votes => Mock.Of<IVoteRepository>();
        public IUserRepository Users => Mock.Of<IUserRepository>();

        public Task SaveChangesAsync()
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider BuildServices(IRepositoryAdapter mySql, IRepositoryAdapter mongo)
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IRepositoryAdapter>("mysql", (_, _) => mySql);
        services.AddKeyedScoped<IRepositoryAdapter>("mongo", (_, _) => mongo);
        return services.BuildServiceProvider();
    }
}

