using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class MongoVoteRepositoryUnitTests
{
    private static IAsyncCursor<T> CursorOf<T>(params T[] items)
    {
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.SetupSequence(c => c.MoveNextAsync(default)).ReturnsAsync(items.Length > 0).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(items);
        return cursor.Object;
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_Returns()
    {
        var id = Guid.NewGuid();
        var col = new Mock<IMongoCollection<Vote>>();
        col.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<Vote>>(), It.IsAny<FindOptions<Vote, Vote>>(), default))
            .ReturnsAsync(CursorOf(new Vote { Id = id }));

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Votes).Returns(col.Object);
        var sut = new MongoVoteRepository(ctx.Object);

        (await sut.GetByIdAsync(id))!.Id.Should().Be(id);
    }
}

