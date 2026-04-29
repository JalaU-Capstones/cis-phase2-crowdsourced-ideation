using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Routing;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

        var sut = new FallbackAdapter(mySql, mongo, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

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

        var sut = new FallbackAdapter(mySql, mongo, fallback.Object, accessor, NullLogger<FallbackAdapter>.Instance);

        sut.Topics.Should().BeSameAs(mongoTopics);
        await sut.SaveChangesAsync();
        mongo.SaveChangesCalls.Should().Be(1);
        mySql.SaveChangesCalls.Should().Be(0);
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
}

