using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class MongoUserRepositoryTests
{
    private static IAsyncCursor<T> CursorOf<T>(params T[] items)
    {
        var cursor = new Mock<IAsyncCursor<T>>();
        cursor.SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(items.Length > 0)
            .ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(items);
        return cursor.Object;
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        var col = new Mock<IMongoCollection<UserRecord>>();
        col.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserRecord>>(),
                It.IsAny<FindOptions<UserRecord, UserRecord>>(),
                default))
            .ReturnsAsync(CursorOf<UserRecord>());

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Users).Returns(col.Object);

        var sut = new MongoUserRepository(ctx.Object);
        (await sut.GetByIdAsync("1")).Should().BeNull();
    }

    [Fact]
    public async Task GetByLoginAsync_WhenNoDoc_ReturnsNull()
    {
        var col = new Mock<IMongoCollection<UserRecord>>();
        col.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserRecord>>(),
                It.IsAny<FindOptions<UserRecord, BsonDocument>>(),
                default))
            .ReturnsAsync(CursorOf<BsonDocument>());

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Users).Returns(col.Object);

        var sut = new MongoUserRepository(ctx.Object);
        (await sut.GetByLoginAsync("someone")).Should().BeNull();
    }

    [Theory]
    [InlineData("string")]
    [InlineData("guid")]
    [InlineData("objectId")]
    [InlineData("other")]
    public async Task GetByLoginAsync_MapsIdLoginNamePassword(string idKind)
    {
        BsonValue idValue = idKind switch
        {
            "string" => "u1",
            "guid" => new BsonBinaryData(Guid.Parse("11111111-1111-1111-1111-111111111111"), GuidRepresentation.Standard),
            "objectId" => ObjectId.GenerateNewId(),
            _ => new BsonInt32(123),
        };

        var doc = new BsonDocument
        {
            ["_id"] = idValue,
            ["login"] = "caseLogin",
            ["name"] = "Case Name",
            ["password"] = "p"
        };

        var col = new Mock<IMongoCollection<UserRecord>>();
        col.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserRecord>>(),
                It.IsAny<FindOptions<UserRecord, BsonDocument>>(),
                default))
            .ReturnsAsync(CursorOf(doc));

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Users).Returns(col.Object);

        var sut = new MongoUserRepository(ctx.Object);
        var result = await sut.GetByLoginAsync("caseLogin");

        result.Should().NotBeNull();
        result!.Login.Should().Be("caseLogin");
        result.Name.Should().Be("Case Name");
        result.Password.Should().Be("p");
        result.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        var u1 = new UserRecord { Id = "1", Login = "l1", Name = "n1", Password = "p" };
        var u2 = new UserRecord { Id = "2", Login = "l2", Name = "n2", Password = "p" };

        var cursor = new Mock<IAsyncCursor<UserRecord>>();
        cursor.SetupSequence(c => c.MoveNextAsync(default)).ReturnsAsync(true).ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(new[] { u1, u2 });

        var col = new Mock<IMongoCollection<UserRecord>>();
        col.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<UserRecord>>(),
                It.IsAny<FindOptions<UserRecord, UserRecord>>(),
                default))
            .ReturnsAsync(cursor.Object);

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Users).Returns(col.Object);
        var sut = new MongoUserRepository(ctx.Object);

        (await sut.GetAllAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task AddUpdateDelete_DelegateToMongoCollection()
    {
        var col = new Mock<IMongoCollection<UserRecord>>();
        col.Setup(c => c.InsertOneAsync(It.IsAny<UserRecord>(), null, default)).Returns(Task.CompletedTask);
        col.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<UserRecord>>(), It.IsAny<UserRecord>(), It.IsAny<ReplaceOptions>(), default))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());
        col.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<UserRecord>>(), default))
            .ReturnsAsync(new DeleteResult.Acknowledged(1));

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Users).Returns(col.Object);
        var sut = new MongoUserRepository(ctx.Object);

        var user = new UserRecord { Id = "1", Login = "l1", Name = "n", Password = "p" };
        await sut.AddAsync(user);
        await sut.UpdateAsync(user);
        await sut.DeleteAsync(user);

        col.Verify(c => c.InsertOneAsync(user, null, default), Times.Once);
        col.Verify(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<UserRecord>>(), user, It.IsAny<ReplaceOptions>(), default), Times.Once);
        col.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<UserRecord>>(), default), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_UsesCountDocumentsAsync()
    {
        var col = new Mock<IMongoCollection<UserRecord>>();
        col.Setup(c => c.CountDocumentsAsync(It.IsAny<ExpressionFilterDefinition<UserRecord>>(), null, default))
            .ReturnsAsync(1L);

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Users).Returns(col.Object);
        var sut = new MongoUserRepository(ctx.Object);

        (await sut.ExistsAsync("1")).Should().BeTrue();
    }

    [Fact]
    public async Task CountAsync_CastsLongToInt()
    {
        var col = new Mock<IMongoCollection<UserRecord>>();
        col.Setup(c => c.CountDocumentsAsync(It.IsAny<ExpressionFilterDefinition<UserRecord>>(), null, default))
            .ReturnsAsync(12L);

        var ctx = new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3");
        ctx.Setup(c => c.Users).Returns(col.Object);
        var sut = new MongoUserRepository(ctx.Object);

        (await sut.CountAsync()).Should().Be(12);
    }
}

