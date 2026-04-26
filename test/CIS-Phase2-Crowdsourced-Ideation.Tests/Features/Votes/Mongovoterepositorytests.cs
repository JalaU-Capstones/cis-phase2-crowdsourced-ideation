using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Moq;
using Xunit;
using FluentAssertions;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Votes;

public sealed class MongoVoteRepositoryTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Vote BuildVote(Guid? ideaId = null) =>
        new() { Id = Guid.NewGuid(), IdeaId = ideaId ?? Guid.NewGuid(), UserId = Guid.NewGuid() };

    private static IAsyncCursor<T> BuildCursor<T>(List<T> items)
    {
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.Setup(c => c.Current).Returns(items);
        cursor.SetupSequence(c => c.MoveNextAsync(default))
              .ReturnsAsync(items.Count > 0)
              .ReturnsAsync(false);
        cursor.Setup(c => c.MoveNext(default)).Returns(false);
        return cursor.Object;
    }

    private static (MongoVoteRepository repo, Mock<IMongoCollection<Vote>> col)
        CreateSut(List<Vote>? seed = null)
    {
        var items = seed ?? new List<Vote>();
        var col   = new Mock<IMongoCollection<Vote>>();
        col.Setup(c => c.FindAsync(
               It.IsAny<FilterDefinition<Vote>>(),
               It.IsAny<FindOptions<Vote, Vote>>(),
               default))
           .ReturnsAsync(BuildCursor(items));

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Votes).Returns(col.Object);

        return (new MongoVoteRepository(db.Object), col);
    }

    // ---------------------------------------------------------------------------
    // GetAllAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var (repo, _) = CreateSut();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenVotesExist_ReturnsAll()
    {
        var (repo, _) = CreateSut(seed: new List<Vote> { BuildVote(), BuildVote() });
        (await repo.GetAllAsync()).Should().HaveCount(2);
    }

    // ---------------------------------------------------------------------------
    // GetByIdeaIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdeaIdAsync_ReturnsVotesForIdea()
    {
        var ideaId = Guid.NewGuid();
        var (repo, _) = CreateSut(seed: new List<Vote> { BuildVote(ideaId: ideaId) });
        var result = await repo.GetByIdeaIdAsync(ideaId);
        result.Should().ContainSingle().Which.IdeaId.Should().Be(ideaId);
    }

    [Fact]
    public async Task GetByIdeaIdAsync_WhenNoVotes_ReturnsEmpty()
    {
        var (repo, _) = CreateSut();
        (await repo.GetByIdeaIdAsync(Guid.NewGuid())).Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // AddAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_CallsInsertOneAsync_Once()
    {
        var (repo, col) = CreateSut();
        var vote = BuildVote();
        await repo.AddAsync(vote);
        col.Verify(c => c.InsertOneAsync(vote, null, default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // DeleteAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_CallsDeleteOneAsync()
    {
        var (repo, col) = CreateSut();
        var vote = BuildVote();
        col.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Vote>>(), default))
           .ReturnsAsync(new DeleteResult.Acknowledged(1));

        await repo.DeleteAsync(vote);

        col.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Vote>>(), default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // ExistsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_WhenVoteExists_ReturnsTrue()
    {
        var vote = BuildVote();
        var col  = new Mock<IMongoCollection<Vote>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Vote>>(), null, default))
           .ReturnsAsync(1L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Votes).Returns(col.Object);

        (await new MongoVoteRepository(db.Object).ExistsAsync(vote.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenVoteDoesNotExist_ReturnsFalse()
    {
        var col = new Mock<IMongoCollection<Vote>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Vote>>(), null, default))
           .ReturnsAsync(0L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Votes).Returns(col.Object);

        (await new MongoVoteRepository(db.Object).ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // CountByIdeaIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CountByIdeaIdAsync_ReturnsCorrectCount()
    {
        var col = new Mock<IMongoCollection<Vote>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Vote>>(), null, default))
           .ReturnsAsync(4L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Votes).Returns(col.Object);

        (await new MongoVoteRepository(db.Object).CountByIdeaIdAsync(Guid.NewGuid())).Should().Be(4);
    }

    // ---------------------------------------------------------------------------
    // CountAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CountAsync_ReturnsCorrectTotalCount()
    {
        var col = new Mock<IMongoCollection<Vote>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Vote>>(), null, default))
           .ReturnsAsync(7L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Votes).Returns(col.Object);

        (await new MongoVoteRepository(db.Object).CountAsync()).Should().Be(7);
    }
}