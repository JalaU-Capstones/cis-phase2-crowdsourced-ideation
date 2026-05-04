using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence;

public sealed class MongoDbAdapterTests
{
    [Fact]
    public void Repositories_AreLazyAndStable()
    {
        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        var sut = new MongoDbAdapter(ctx.Object);

        var topics1 = sut.Topics;
        var topics2 = sut.Topics;
        topics1.Should().BeSameAs(topics2);
        topics1.Should().BeOfType<MongoTopicRepository>();

        sut.Ideas.Should().BeOfType<MongoIdeaRepository>();
        sut.Votes.Should().BeOfType<MongoVoteRepository>();
        sut.Users.Should().BeOfType<MongoUserRepository>();
    }

    [Fact]
    public async Task SaveChangesAsync_IsNoOpCompletedTask()
    {
        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        var sut = new MongoDbAdapter(ctx.Object);

        await sut.SaveChangesAsync();
    }
}

