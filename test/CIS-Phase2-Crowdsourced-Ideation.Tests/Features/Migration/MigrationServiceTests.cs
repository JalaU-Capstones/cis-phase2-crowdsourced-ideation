using CIS.Phase2.CrowdsourcedIdeation.Tests.Migration;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;
using Testcontainers.MongoDb;
using Testcontainers.MySql;
using Xunit;
using Dapper;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Migration;

/// <summary>
/// Integration tests for MigrationService using Testcontainers.
///
/// Responsibility boundary tested:
///   - C# migration NEVER writes to the `users` collection (owned by Java Phase 1).
///   - Pre-validation fails if referenced user IDs are missing in MongoDB.
///   - Only topics, ideas and votes are migrated.
///
/// Run locally with Docker:
///   dotnet test --filter "FullyQualifiedName~Migration"
/// </summary>
[Collection("Migration")]
[Trait("Category", "DockerRequired")]
public sealed class MigrationServiceTests : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithDatabase("sd3")
        .WithUsername("sd3user")
        .WithPassword("sd3pass")
        .WithImage("mysql:8.0")
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:6.0")
        .Build();

    private string MysqlConnStr => _mysql.GetConnectionString();
    private string MongoConnStr => _mongo.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _mongo.StartAsync());
        await CreateMySqlSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_mysql.StopAsync(), _mongo.StopAsync());
    }

    // ---------------------------------------------------------------------------
    // Schema + seed helpers
    // ---------------------------------------------------------------------------

    private async Task CreateMySqlSchemaAsync()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS users (
                id       VARCHAR(36) PRIMARY KEY,
                login    VARCHAR(20) NOT NULL UNIQUE,
                name     VARCHAR(200) NOT NULL,
                password VARCHAR(100) NOT NULL
            );
            CREATE TABLE IF NOT EXISTS topics (
                id          VARCHAR(36) PRIMARY KEY,
                title       VARCHAR(200) NOT NULL,
                description TEXT,
                status      VARCHAR(10) NOT NULL DEFAULT 'OPEN',
                owner_id    VARCHAR(36) NOT NULL,
                created_at  DATETIME NOT NULL,
                updated_at  DATETIME NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ideas (
                id         VARCHAR(36) PRIMARY KEY,
                topic_id   VARCHAR(36) NOT NULL,
                owner_id   VARCHAR(36) NOT NULL,
                content    TEXT NOT NULL,
                created_at DATETIME NOT NULL,
                updated_at DATETIME NOT NULL
            );
            CREATE TABLE IF NOT EXISTS votes (
                id      VARCHAR(36) PRIMARY KEY,
                idea_id VARCHAR(36) NOT NULL,
                user_id VARCHAR(36) NOT NULL
            );
            """);
    }

    /// <summary>Seeds users in BOTH MySQL and MongoDB (simulating Phase 1 Java migration).</summary>
    private async Task<List<string>> SeedUsersAsync(MySqlConnection mysql, IMongoDatabase mongoDb, int count = 2)
    {
        var ids = new List<string>();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        for (int i = 0; i < count; i++)
        {
            var id = Guid.NewGuid().ToString();
            ids.Add(id);

            // MySQL (Phase 1 source)
            await mysql.ExecuteAsync(
                "INSERT INTO users (id, login, name, password) VALUES (@id, @login, @name, @pwd)",
                new { id, login = $"user{i}_{id[..8]}", name = $"User {i}", pwd = "hash" });

            // MongoDB (Phase 1 already migrated — simulated here)
            await mongoDb.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument
            {
                ["_id"]      = id,
                ["Login"]    = $"user{i}_{id[..8]}",
                ["Name"]     = $"User {i}",
                ["Password"] = "hash"
            });
        }

        return ids;
    }

    private async Task SeedTopicsAsync(MySqlConnection mysql, List<string> userIds, int count = 2)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        for (int i = 0; i < count; i++)
        {
            await mysql.ExecuteAsync(
                "INSERT INTO topics (id, title, description, status, owner_id, created_at, updated_at) VALUES (@id, @title, @desc, 'OPEN', @oid, @now, @now)",
                new { id = Guid.NewGuid().ToString(), title = $"Topic {i}", desc = $"Desc {i}", oid = userIds[i % userIds.Count], now });
        }
    }

    private async Task<List<string>> SeedIdeasAsync(MySqlConnection mysql, List<string> userIds, List<string> topicIds, int count = 3)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var ids = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var id = Guid.NewGuid().ToString();
            ids.Add(id);
            await mysql.ExecuteAsync(
                "INSERT INTO ideas (id, topic_id, owner_id, content, created_at, updated_at) VALUES (@id, @tid, @oid, @content, @now, @now)",
                new { id, tid = topicIds[i % topicIds.Count], oid = userIds[i % userIds.Count], content = $"{{\"title\":\"Idea {i}\",\"description\":\"Desc\",\"isWinning\":false}}", now });
        }
        return ids;
    }

    private async Task SeedVotesAsync(MySqlConnection mysql, List<string> userIds, List<string> ideaIds, int count = 4)
    {
        for (int i = 0; i < count; i++)
        {
            await mysql.ExecuteAsync(
                "INSERT INTO votes (id, idea_id, user_id) VALUES (@id, @iid, @uid)",
                new { id = Guid.NewGuid().ToString(), iid = ideaIds[i % ideaIds.Count], uid = userIds[i % userIds.Count] });
        }
    }

    private MigrationService CreateSut() => new(MysqlConnStr, MongoConnStr, "sd3");
    private IMongoDatabase GetMongoDb() => new MongoClient(MongoConnStr).GetDatabase("sd3");

    // ---------------------------------------------------------------------------
    // Pre-validation tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WhenUsersNotMigratedByPhase1_ThrowsWithClearMessage()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        // Seed MySQL users but do NOT put them in MongoDB (Phase 1 not run yet)
        var userId = Guid.NewGuid().ToString();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, login, name, password) VALUES (@id, @login, @name, @pwd)",
            new { id = userId, login = "orphan", name = "Orphan", pwd = "x" });
        await SeedTopicsAsync(conn, new List<string> { userId }, count: 1);

        var sut = CreateSut();
        var act = async () => await sut.RunAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Missing users in MongoDB*Please run Phase 1 user migration first*");
    }

    [Fact]
    public async Task ValidateMissingUsersAsync_WhenAllUsersExistInMongo_ReturnsEmpty()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 2);
        await SeedTopicsAsync(conn, userIds, count: 1);

        var sut     = CreateSut();
        var missing = await sut.ValidateMissingUsersAsync(conn);

        missing.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateMissingUsersAsync_WhenSomeUsersMissing_ReturnsMissingIds()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var missingId = Guid.NewGuid().ToString();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, login, name, password) VALUES (@id, @login, @name, @pwd)",
            new { id = missingId, login = "missing", name = "Missing", pwd = "x" });
        await SeedTopicsAsync(conn, new List<string> { missingId }, count: 1);

        // Do NOT add this user to MongoDB

        var sut     = CreateSut();
        var missing = await sut.ValidateMissingUsersAsync(conn);

        missing.Should().ContainSingle().Which.Should().Be(missingId);
    }

    // ---------------------------------------------------------------------------
    // Users collection isolation tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_NeverWritesToUsersCollection()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 2);
        await SeedTopicsAsync(conn, userIds, count: 2);
        var topicIds = (await conn.QueryAsync<string>("SELECT id FROM topics")).ToList();
        var ideaIds  = await SeedIdeasAsync(conn, userIds, topicIds, count: 3);
        await SeedVotesAsync(conn, userIds, ideaIds, count: 4);

        var userCountBefore = await mongoDb.GetCollection<BsonDocument>("users")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        var sut = CreateSut();
        await sut.RunAsync();

        var userCountAfter = await mongoDb.GetCollection<BsonDocument>("users")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        userCountAfter.Should().Be(userCountBefore,
            "C# migration must never write to the users collection — that is owned by Java Phase 1");
    }

    // ---------------------------------------------------------------------------
    // Migration correctness tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithEmptyDatabase_ReturnsZeroCounts()
    {
        var sut    = CreateSut();
        var result = await sut.RunAsync();

        result.MigratedTopics.Should().Be(0);
        result.MigratedIdeas.Should().Be(0);
        result.MigratedVotes.Should().Be(0);
        result.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WithData_MigratesTopicsIdeasAndVotesCorrectly()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 2);
        await SeedTopicsAsync(conn, userIds, count: 2);
        var topicIds = (await conn.QueryAsync<string>("SELECT id FROM topics")).ToList();
        var ideaIds  = await SeedIdeasAsync(conn, userIds, topicIds, count: 3);
        await SeedVotesAsync(conn, userIds, ideaIds, count: 4);

        var sut    = CreateSut();
        var result = await sut.RunAsync();

        result.MigratedTopics.Should().Be(2);
        result.MigratedIdeas.Should().Be(3);
        result.MigratedVotes.Should().Be(4);
        result.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_RunningTwiceDoesNotDuplicateDocuments()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 2);
        await SeedTopicsAsync(conn, userIds, count: 2);
        var topicIds = (await conn.QueryAsync<string>("SELECT id FROM topics")).ToList();
        var ideaIds  = await SeedIdeasAsync(conn, userIds, topicIds, count: 3);
        await SeedVotesAsync(conn, userIds, ideaIds, count: 4);

        var sut = CreateSut();
        await sut.RunAsync();
        await sut.RunAsync(); // second run

        var db = GetMongoDb();
        (await db.GetCollection<BsonDocument>("topics").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(2);
        (await db.GetCollection<BsonDocument>("ideas").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(3);
        (await db.GetCollection<BsonDocument>("votes").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).Should().Be(4);
    }

    [Fact]
    public async Task MigrateTopicsAsync_PreservesNullDescription()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 1);
        var topicId = Guid.NewGuid().ToString();
        var now     = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "INSERT INTO topics (id, title, description, status, owner_id, created_at, updated_at) VALUES (@id, @title, NULL, 'OPEN', @oid, @now, @now)",
            new { id = topicId, title = "No desc", oid = userIds[0], now });

        var sut = CreateSut();
        await sut.MigrateTopicsAsync(conn);

        var doc = await GetMongoDb().GetCollection<BsonDocument>("topics")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", topicId))
            .FirstOrDefaultAsync();

        doc.Should().NotBeNull();
        doc["Description"].BsonType.Should().Be(BsonType.Null);
    }

    [Fact]
    public async Task MigrateIdeasAsync_PreservesContentJson()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 1);
        var topicId = Guid.NewGuid().ToString();
        var ideaId  = Guid.NewGuid().ToString();
        var now     = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var content = "{\"title\":\"My Idea\",\"description\":\"My Desc\",\"isWinning\":true}";

        await conn.ExecuteAsync(
            "INSERT INTO topics (id, title, description, status, owner_id, created_at, updated_at) VALUES (@id, 'T', NULL, 'OPEN', @oid, @now, @now)",
            new { id = topicId, oid = userIds[0], now });
        await conn.ExecuteAsync(
            "INSERT INTO ideas (id, topic_id, owner_id, content, created_at, updated_at) VALUES (@id, @tid, @oid, @content, @now, @now)",
            new { id = ideaId, tid = topicId, oid = userIds[0], content, now });

        var sut = CreateSut();
        await sut.MigrateIdeasAsync(conn);

        var doc = await GetMongoDb().GetCollection<BsonDocument>("ideas")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ideaId))
            .FirstOrDefaultAsync();

        doc.Should().NotBeNull();
        doc["Content"].AsString.Should().Be(content);
    }

    [Fact]
    public async Task ValidateAsync_WhenAllMigrated_ReturnsConsistent()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 2);
        await SeedTopicsAsync(conn, userIds, count: 2);
        var topicIds = (await conn.QueryAsync<string>("SELECT id FROM topics")).ToList();
        var ideaIds  = await SeedIdeasAsync(conn, userIds, topicIds, count: 3);
        await SeedVotesAsync(conn, userIds, ideaIds, count: 4);

        var sut = CreateSut();
        await sut.RunAsync();

        var validation = await sut.ValidateAsync(conn);
        validation.IsConsistent.Should().BeTrue();
        validation.Topics.IsMatch.Should().BeTrue();
        validation.Ideas.IsMatch.Should().BeTrue();
        validation.Votes.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenNotMigrated_ReturnsInconsistent()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var mongoDb = GetMongoDb();
        var userIds = await SeedUsersAsync(conn, mongoDb, count: 2);
        await SeedTopicsAsync(conn, userIds, count: 2);

        // No migration run
        var sut        = CreateSut();
        var validation = await sut.ValidateAsync(conn);

        validation.IsConsistent.Should().BeFalse();
        validation.Topics.MySql.Should().Be(2);
        validation.Topics.Mongo.Should().Be(0);
    }
}