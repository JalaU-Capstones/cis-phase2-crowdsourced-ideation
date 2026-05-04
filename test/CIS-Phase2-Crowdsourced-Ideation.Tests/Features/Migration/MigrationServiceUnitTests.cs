using CIS.Phase2.CrowdsourcedIdeation.Features.Migration;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Dapper;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Data;
using System.Data.Common;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Migration;

public sealed class MigrationServiceUnitTests
{
    private static async Task<T> InvokePrivateAsync<T>(MigrationService sut, string methodName, params object?[] args)
    {
        var m = typeof(MigrationService).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        m.Should().NotBeNull();
        return await (Task<T>)m!.Invoke(sut, args)!;
    }

    [Fact]
    public void Ctor_WhenMySqlConnectionMissing_Throws()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MongoDbConnection"] = "mongodb://localhost/unit_test_db"
            })
            .Build();

        var act = () => _ = new MigrationService(cfg);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DefaultConnection*not configured*");
    }

    [Fact]
    public void Ctor_WhenMongoConnectionMissing_Throws()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;User Id=u;Password=p;Database=db;"
            })
            .Build();

        var act = () => _ = new MigrationService(cfg);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MongoDbConnection*not configured*");
    }

    [Fact]
    public void Ctor_WhenMongoConnectionHasNoDatabaseName_Throws()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;User Id=u;Password=p;Database=db;",
                ["ConnectionStrings:MongoDbConnection"] = "mongodb://localhost:27017"
            })
            .Build();

        var act = () => _ = new MigrationService(cfg);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must include a database name*");
    }

    [Fact]
    public void Records_HaveExpectedConsistencySemantics()
    {
        new CountPair(1, 1).IsMatch.Should().BeTrue();
        new CountPair(1, 2).IsMatch.Should().BeFalse();

        new ValidationResult(new CountPair(1, 1), new CountPair(2, 2), new CountPair(3, 3)).IsConsistent.Should().BeTrue();
        new ValidationResult(new CountPair(1, 0), new CountPair(2, 2), new CountPair(3, 3)).IsConsistent.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateMissingUsersAsync_WhenNoReferences_ReturnsEmpty_AndDoesNotHitMongo()
    {
        var mysql = new Mock<DbConnection>();
        mysql.SetupDapperAsync(c => c.QueryAsync<string>(It.IsAny<CommandDefinition>()))
            .ReturnsAsync(Array.Empty<string>());

        var mongo = new Mock<IMongoDatabase>();

        var sut = new MigrationService("cs", mongo.Object);
        var result = await sut.ValidateMissingUsersAsync(mysql.Object);

        result.Should().BeEmpty();
        mongo.Verify(m => m.GetCollection<BsonDocument>(It.IsAny<string>(), null), Times.Never);
    }

    [Fact]
    public async Task ValidateMissingUsersAsync_WhenSomeUsersMissing_ReturnsMissingIds()
    {
        var referenced = new[] { "u1", "u2", "u3" };

        var mysql = new Mock<DbConnection>();
        mysql.SetupDapperAsync(c => c.QueryAsync<string>(It.IsAny<CommandDefinition>()))
            .ReturnsAsync(referenced);

        // Mongo users cursor returns only u1 and u3 => u2 is missing.
        var usersCursor = new Mock<IAsyncCursor<BsonDocument>>();
        usersCursor.SetupSequence(c => c.MoveNextAsync(default)).ReturnsAsync(true).ReturnsAsync(false);
        usersCursor.SetupGet(c => c.Current).Returns(new[]
        {
            new BsonDocument { ["_id"] = referenced[0] },
            new BsonDocument { ["_id"] = referenced[2] }
        });

        var usersCol = new Mock<IMongoCollection<BsonDocument>>();
        usersCol.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
                default))
            .ReturnsAsync(usersCursor.Object);

        var mongo = new Mock<IMongoDatabase>();
        mongo.Setup(m => m.GetCollection<BsonDocument>("users", null)).Returns(usersCol.Object);

        var sut = new MigrationService("cs", mongo.Object);
        var result = await sut.ValidateMissingUsersAsync(mysql.Object);

        result.Should().ContainSingle().Which.Should().Be(referenced[1]);
    }

    [Fact]
    public async Task MigrateTopicsAsync_WritesDocs_AndCounts()
    {
        var mysql = new Mock<DbConnection>();
        mysql.SetupDapperAsync(c => c.QueryAsync<MigrationService.TopicRow>(It.IsAny<CommandDefinition>()))
            .ReturnsAsync(new[]
            {
                new MigrationService.TopicRow
                {
                    id = "t1",
                    title = "T1",
                    description = null,
                    status = "OPEN",
                    owner_id = "u1",
                    created_at = new DateTime(2020,1,1),
                    updated_at = new DateTime(2020,1,2)
                }
            });

        var col = new Mock<IMongoCollection<BsonDocument>>();
        col.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<BsonDocument>(), It.IsAny<ReplaceOptions>(), default))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());

        var mongo = new Mock<IMongoDatabase>();
        mongo.Setup(m => m.GetCollection<BsonDocument>("topics", null)).Returns(col.Object);

        var sut = new MigrationService("cs", mongo.Object);
        (await InvokePrivateAsync<long>(sut, "MigrateTopicsAsync", mysql.Object)).Should().Be(1);
        col.Verify(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<BsonDocument>(), It.IsAny<ReplaceOptions>(), default), Times.Once);
    }

    [Fact]
    public async Task MigrateIdeasAsync_WritesDocs_AndCounts()
    {
        var mysql = new Mock<DbConnection>();
        mysql.SetupDapperAsync(c => c.QueryAsync<MigrationService.IdeaRow>(It.IsAny<CommandDefinition>()))
            .ReturnsAsync(new[]
            {
                new MigrationService.IdeaRow
                {
                    id = "i1",
                    topic_id = "t1",
                    owner_id = "u1",
                    content = "{}",
                    created_at = new DateTime(2020,1,1),
                    updated_at = new DateTime(2020,1,2)
                }
            });

        var col = new Mock<IMongoCollection<BsonDocument>>();
        col.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<BsonDocument>(), It.IsAny<ReplaceOptions>(), default))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());

        var mongo = new Mock<IMongoDatabase>();
        mongo.Setup(m => m.GetCollection<BsonDocument>("ideas", null)).Returns(col.Object);

        var sut = new MigrationService("cs", mongo.Object);
        (await InvokePrivateAsync<long>(sut, "MigrateIdeasAsync", mysql.Object)).Should().Be(1);
    }

    [Fact]
    public async Task MigrateVotesAsync_WritesDocs_AndCounts()
    {
        var mysql = new Mock<DbConnection>();
        mysql.SetupDapperAsync(c => c.QueryAsync<MigrationService.VoteRow>(It.IsAny<CommandDefinition>()))
            .ReturnsAsync(new[]
            {
                new MigrationService.VoteRow { id = "v1", idea_id = "i1", user_id = "u1" }
            });

        var col = new Mock<IMongoCollection<BsonDocument>>();
        col.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<BsonDocument>(), It.IsAny<ReplaceOptions>(), default))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());

        var mongo = new Mock<IMongoDatabase>();
        mongo.Setup(m => m.GetCollection<BsonDocument>("votes", null)).Returns(col.Object);

        var sut = new MigrationService("cs", mongo.Object);
        (await InvokePrivateAsync<long>(sut, "MigrateVotesAsync", mysql.Object)).Should().Be(1);
    }

    [Fact]
    public async Task ValidateAsync_WhenMismatch_IsInconsistent()
    {
        var mysql = new Mock<DbConnection>();
        mysql.SetupDapperAsync(c => c.ExecuteScalarAsync<long>(It.IsAny<CommandDefinition>()))
            .ReturnsAsync(2L);

        var col = new Mock<IMongoCollection<BsonDocument>>();
        col.Setup(c => c.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, null, default))
            .ReturnsAsync(1L);

        var mongo = new Mock<IMongoDatabase>();
        mongo.Setup(m => m.GetCollection<BsonDocument>(It.IsAny<string>(), null)).Returns(col.Object);

        var sut = new MigrationService("cs", mongo.Object);
        (await sut.ValidateAsync(mysql.Object)).IsConsistent.Should().BeFalse();
    }
}
