using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Moq;
using Xunit;
using FluentAssertions;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Topics;

public sealed class MongoTopicRepositoryTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Topic BuildTopic(string? id = null, TopicStatus status = TopicStatus.OPEN) =>
        new()
        {
            Id        = id ?? Guid.NewGuid().ToString(),
            Title     = "Test Topic",
            OwnerId   = Guid.NewGuid().ToString(),
            Status    = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    /// <summary>
    /// Construye un IAsyncCursor correctamente mockeado.
    /// Current debe estar listo ANTES del primer MoveNextAsync (requisito del driver real).
    /// </summary>
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

    private static (MongoTopicRepository repo, Mock<IMongoCollection<Topic>> col)
        CreateSut(List<Topic>? seed = null)
    {
        var items = seed ?? new List<Topic>();
        var col   = new Mock<IMongoCollection<Topic>>();
        col.Setup(c => c.FindAsync(
               It.IsAny<FilterDefinition<Topic>>(),
               It.IsAny<FindOptions<Topic, Topic>>(),
               default))
           .ReturnsAsync(BuildCursor(items));

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Topics).Returns(col.Object);

        return (new MongoTopicRepository(db.Object), col);
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
    public async Task GetAllAsync_WhenTopicsExist_ReturnsAll()
    {
        var (repo, _) = CreateSut(seed: new List<Topic> { BuildTopic(), BuildTopic() });
        (await repo.GetAllAsync()).Should().HaveCount(2);
    }

    // ---------------------------------------------------------------------------
    // AddAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_CallsInsertOneAsync_Once()
    {
        var (repo, col) = CreateSut();
        var topic = BuildTopic();
        await repo.AddAsync(topic);
        col.Verify(c => c.InsertOneAsync(topic, null, default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // UpdateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_CallsReplaceOneAsync()
    {
        var (repo, col) = CreateSut();
        var topic = BuildTopic();
        col.Setup(c => c.ReplaceOneAsync(
               It.IsAny<FilterDefinition<Topic>>(), topic,
               It.IsAny<ReplaceOptions>(), default))
           .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

        await repo.UpdateAsync(topic);

        col.Verify(c => c.ReplaceOneAsync(
            It.IsAny<FilterDefinition<Topic>>(), topic,
            It.IsAny<ReplaceOptions>(), default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // DeleteAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_CallsDeleteOneAsync()
    {
        var (repo, col) = CreateSut();
        var topic = BuildTopic();
        col.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Topic>>(), default))
           .ReturnsAsync(new DeleteResult.Acknowledged(1));

        await repo.DeleteAsync(topic);

        col.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Topic>>(), default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // ExistsAsync — cursor construido ad-hoc por test para evitar reutilización
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_WhenTopicExists_ReturnsTrue()
    {
        var topic = BuildTopic();
        var col   = new Mock<IMongoCollection<Topic>>();
        // ExistsAsync usa CountDocumentsAsync > 0 (no AnyAsync)
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Topic>>(), null, default))
           .ReturnsAsync(1L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Topics).Returns(col.Object);

        (await new MongoTopicRepository(db.Object).ExistsAsync(topic.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenTopicDoesNotExist_ReturnsFalse()
    {
        var col = new Mock<IMongoCollection<Topic>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Topic>>(), null, default))
           .ReturnsAsync(0L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Topics).Returns(col.Object);

        (await new MongoTopicRepository(db.Object).ExistsAsync(Guid.NewGuid().ToString())).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // GetFilteredAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetFilteredAsync_WithValidStatus_ReturnsMatchingTopics()
    {
        var (repo, _) = CreateSut(seed: new List<Topic> { BuildTopic(status: TopicStatus.OPEN) });
        var result = await repo.GetFilteredAsync("OPEN", null);
        result.Should().ContainSingle().Which.Status.Should().Be(TopicStatus.OPEN);
    }

    [Fact]
    public async Task GetFilteredAsync_WithInvalidStatus_ReturnsAllTopics()
    {
        var (repo, _) = CreateSut(seed: new List<Topic> { BuildTopic(), BuildTopic() });
        (await repo.GetFilteredAsync("INVALID", null)).Should().HaveCount(2);
    }

    // ---------------------------------------------------------------------------
    // CountAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        var col = new Mock<IMongoCollection<Topic>>();
        col.Setup(c => c.CountDocumentsAsync(
               It.IsAny<FilterDefinition<Topic>>(), null, default))
           .ReturnsAsync(5L);

        var db = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        db.Setup(d => d.Topics).Returns(col.Object);

        (await new MongoTopicRepository(db.Object).CountAsync()).Should().Be(5);
    }
}