using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class MongoIdeaRepositoryTests
{
    private static IAsyncCursor<T> CursorOf<T>(params T[] items)
    {
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.SetupSequence(c => c.MoveNextAsync(default)).ReturnsAsync(items.Length > 0).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(items);
        return cursor.Object;
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        var col = new Mock<IMongoCollection<Idea>>();
        col.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Idea>>(), It.IsAny<FindOptions<Idea, Idea>>(), default))
            .ReturnsAsync(CursorOf<Idea>());

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Ideas).Returns(col.Object);

        var sut = new MongoIdeaRepository(ctx.Object);
        (await sut.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll()
    {
        var col = new Mock<IMongoCollection<Idea>>();
        col.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Idea>>(), It.IsAny<FindOptions<Idea, Idea>>(), default))
            .ReturnsAsync(CursorOf(new Idea { Id = Guid.NewGuid() }, new Idea { Id = Guid.NewGuid() }));

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Ideas).Returns(col.Object);
        var sut = new MongoIdeaRepository(ctx.Object);

        (await sut.GetAllAsync()).Should().HaveCount(2);
    }
}

