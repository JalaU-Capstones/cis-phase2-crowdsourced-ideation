using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class MongoTopicRepositoryTests
{
    private static IAsyncCursor<T> CursorOf<T>(params T[] items)
    {
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.SetupSequence(c => c.MoveNextAsync(default)).ReturnsAsync(items.Length > 0).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(items);
        return cursor.Object;
    }

    [Fact]
    public async Task GetFilteredAsync_BuildsFilterAndReturns()
    {
        var col = new Mock<IMongoCollection<Topic>>();
        col.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Topic>>(), It.IsAny<FindOptions<Topic, Topic>>(), default))
            .ReturnsAsync(CursorOf(new Topic { Id = "t1" }));

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Topics).Returns(col.Object);

        var sut = new MongoTopicRepository(ctx.Object);
        var result = await sut.GetFilteredAsync(status: "CLOSED", ownerId: "u1");

        result.Should().ContainSingle().Which.Id.Should().Be("t1");
        col.Verify(c => c.FindAsync(It.IsAny<FilterDefinition<Topic>>(), It.IsAny<FindOptions<Topic, Topic>>(), default), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        var col = new Mock<IMongoCollection<Topic>>();
        col.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Topic>>(), It.IsAny<FindOptions<Topic, Topic>>(), default))
            .ReturnsAsync(CursorOf<Topic>());

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Topics).Returns(col.Object);
        var sut = new MongoTopicRepository(ctx.Object);

        (await sut.GetByIdAsync("nope")).Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_TrueWhenCountPositive()
    {
        var col = new Mock<IMongoCollection<Topic>>();
        col.Setup(c => c.CountDocumentsAsync(It.IsAny<FilterDefinition<Topic>>(), null, default)).ReturnsAsync(1L);

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Topics).Returns(col.Object);
        var sut = new MongoTopicRepository(ctx.Object);

        (await sut.ExistsAsync("t1")).Should().BeTrue();
    }

    [Fact]
    public async Task CountAsync_CastsLongToInt()
    {
        var col = new Mock<IMongoCollection<Topic>>();
        col.Setup(c => c.CountDocumentsAsync(It.IsAny<FilterDefinition<Topic>>(), null, default)).ReturnsAsync(5L);

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Topics).Returns(col.Object);
        var sut = new MongoTopicRepository(ctx.Object);

        (await sut.CountAsync()).Should().Be(5);
    }
}
