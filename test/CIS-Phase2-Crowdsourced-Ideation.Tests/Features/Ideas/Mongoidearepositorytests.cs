using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Moq;
using Xunit;
using FluentAssertions;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Ideas;

public sealed class MongoIdeaRepositoryTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Idea BuildIdea(string? topicId = null) =>
        new()
        {
            Id          = Guid.NewGuid(),
            TopicId     = topicId ?? Guid.NewGuid().ToString(),
            OwnerId     = Guid.NewGuid(),
            Title       = "Test Idea",
            Description = "Test Description",
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

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

    private static (MongoIdeaRepository repo, Mock<IMongoCollection<Idea>> col)
        CreateSut(List<Idea>? seed = null)
    {
        var items = seed ?? new List<Idea>();
        var col   = new Mock<IMongoCollection<Idea>>();
        col.Setup(c => c.FindAsync(
               It.IsAny<FilterDefinition<Idea>>(),
               It.IsAny<FindOptions<Idea, Idea>>(),
               default))
           .ReturnsAsync(BuildCursor(items));

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Ideas).Returns(col.Object);

        return (new MongoIdeaRepository(db.Object), col);
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
    public async Task GetAllAsync_WhenIdeasExist_ReturnsAll()
    {
        var (repo, _) = CreateSut(seed: new List<Idea> { BuildIdea(), BuildIdea(), BuildIdea() });
        (await repo.GetAllAsync()).Should().HaveCount(3);
    }

    // ---------------------------------------------------------------------------
    // GetByTopicIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByTopicIdAsync_ReturnsIdeasForTopic()
    {
        var topicId = Guid.NewGuid().ToString();
        var (repo, _) = CreateSut(seed: new List<Idea> { BuildIdea(topicId: topicId) });
        var result = await repo.GetByTopicIdAsync(topicId);
        result.Should().ContainSingle().Which.TopicId.Should().Be(topicId);
    }

    [Fact]
    public async Task GetByTopicIdAsync_WhenNoIdeasForTopic_ReturnsEmpty()
    {
        var (repo, _) = CreateSut();
        (await repo.GetByTopicIdAsync(Guid.NewGuid().ToString())).Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // AddAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_CallsInsertOneAsync_Once()
    {
        var (repo, col) = CreateSut();
        var idea = BuildIdea();
        await repo.AddAsync(idea);
        col.Verify(c => c.InsertOneAsync(idea, null, default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // UpdateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_CallsReplaceOneAsync()
    {
        var (repo, col) = CreateSut();
        var idea = BuildIdea();
        col.Setup(c => c.ReplaceOneAsync(
               It.IsAny<FilterDefinition<Idea>>(), idea,
               It.IsAny<ReplaceOptions>(), default))
           .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

        await repo.UpdateAsync(idea);

        col.Verify(c => c.ReplaceOneAsync(
            It.IsAny<FilterDefinition<Idea>>(), idea,
            It.IsAny<ReplaceOptions>(), default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // DeleteAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_CallsDeleteOneAsync()
    {
        var (repo, col) = CreateSut();
        var idea = BuildIdea();
        col.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Idea>>(), default))
           .ReturnsAsync(new DeleteResult.Acknowledged(1));

        await repo.DeleteAsync(idea);

        col.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Idea>>(), default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // ExistsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_WhenIdeaExists_ReturnsTrue()
    {
        var idea = BuildIdea();
        var col  = new Mock<IMongoCollection<Idea>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Idea>>(), null, default))
           .ReturnsAsync(1L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Ideas).Returns(col.Object);

        (await new MongoIdeaRepository(db.Object).ExistsAsync(idea.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenIdeaDoesNotExist_ReturnsFalse()
    {
        var col = new Mock<IMongoCollection<Idea>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Idea>>(), null, default))
           .ReturnsAsync(0L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Ideas).Returns(col.Object);

        (await new MongoIdeaRepository(db.Object).ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // CountAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        var col = new Mock<IMongoCollection<Idea>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Idea>>(), null, default))
           .ReturnsAsync(3L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Ideas).Returns(col.Object);

        (await new MongoIdeaRepository(db.Object).CountAsync()).Should().Be(3);
    }
}