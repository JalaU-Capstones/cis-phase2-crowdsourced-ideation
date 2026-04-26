using CIS.Phase2.CrowdsourcedIdeation.Tests.Migration;
using DotNet.Testcontainers.Builders;
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
/// Tests de integración para MigrationService.
/// Levanta contenedores reales de MySQL y MongoDB usando Testcontainers.
/// Verifica que la migración transfiere datos correctamente y es idempotente.
///
/// IMPORTANTE: estos tests requieren Docker local. Se excluyen del pipeline de CI
/// usando el trait "Category=DockerRequired". Para correrlos localmente:
///   dotnet test --filter "FullyQualifiedName~Migration"
/// </summary>
[Collection("Migration")]
[Trait("Category", "DockerRequired")]
public sealed class MigrationServiceTests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // Contenedores — el endpoint se detecta automáticamente por Testcontainers.
    // En Windows usa npipe, en Linux usa /var/run/docker.sock.
    // ---------------------------------------------------------------------------

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

    // ---------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------

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
    // Schema setup
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

    private async Task SeedMySqlAsync(MySqlConnection conn,
        int userCount = 2, int topicCount = 2, int ideaCount = 3, int voteCount = 4)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        for (int i = 0; i < userCount; i++)
        {
            var id = Guid.NewGuid().ToString();
            await conn.ExecuteAsync(
                "INSERT INTO users (id, login, name, password) VALUES (@id, @login, @name, @pwd)",
                new { id, login = $"user{i}_{id[..8]}", name = $"User {i}", pwd = "hash" });
        }

        var userId = (await conn.QueryFirstAsync<string>("SELECT id FROM users LIMIT 1"));

        var topicIds = new List<string>();
        for (int i = 0; i < topicCount; i++)
        {
            var id = Guid.NewGuid().ToString();
            topicIds.Add(id);
            await conn.ExecuteAsync(
                "INSERT INTO topics (id, title, description, status, owner_id, created_at, updated_at) VALUES (@id, @title, @desc, 'OPEN', @oid, @now, @now)",
                new { id, title = $"Topic {i}", desc = $"Desc {i}", oid = userId, now });
        }

        var ideaIds = new List<string>();
        for (int i = 0; i < ideaCount; i++)
        {
            var id     = Guid.NewGuid().ToString();
            var topicId = topicIds[i % topicIds.Count];
            ideaIds.Add(id);
            await conn.ExecuteAsync(
                "INSERT INTO ideas (id, topic_id, owner_id, content, created_at, updated_at) VALUES (@id, @tid, @oid, @content, @now, @now)",
                new { id, tid = topicId, oid = userId, content = $"{{\"title\":\"Idea {i}\",\"description\":\"Desc\",\"isWinning\":false}}", now });
        }

        for (int i = 0; i < voteCount; i++)
        {
            var id     = Guid.NewGuid().ToString();
            var ideaId = ideaIds[i % ideaIds.Count];
            await conn.ExecuteAsync(
                "INSERT INTO votes (id, idea_id, user_id) VALUES (@id, @iid, @uid)",
                new { id, iid = ideaId, uid = userId });
        }
    }

    // ---------------------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------------------

    private MigrationService CreateSut() =>
        new(MysqlConnStr, MongoConnStr, "sd3");

    private IMongoDatabase GetMongoDb()
    {
        var client = new MongoClient(MongoConnStr);
        return client.GetDatabase("sd3");
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithEmptyDatabase_ReturnsZeroCounts()
    {
        var sut    = CreateSut();
        var result = await sut.RunAsync();

        result.MigratedUsers.Should().Be(0);
        result.MigratedTopics.Should().Be(0);
        result.MigratedIdeas.Should().Be(0);
        result.MigratedVotes.Should().Be(0);
        result.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public async Task MigrateUsersAsync_TransfersAllUsers()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 3, topicCount: 0, ideaCount: 0, voteCount: 0);

        var sut   = CreateSut();
        var count = await sut.MigrateUsersAsync(conn);

        count.Should().Be(3);

        var mongoCount = await GetMongoDb()
            .GetCollection<BsonDocument>("users")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        mongoCount.Should().Be(3);
    }

    [Fact]
    public async Task MigrateTopicsAsync_TransfersAllTopics()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 1, topicCount: 3, ideaCount: 0, voteCount: 0);

        var sut   = CreateSut();
        await sut.MigrateUsersAsync(conn);
        var count = await sut.MigrateTopicsAsync(conn);

        count.Should().Be(3);

        var mongoCount = await GetMongoDb()
            .GetCollection<BsonDocument>("topics")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        mongoCount.Should().Be(3);
    }

    [Fact]
    public async Task MigrateIdeasAsync_TransfersAllIdeas()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 1, topicCount: 2, ideaCount: 4, voteCount: 0);

        var sut = CreateSut();
        await sut.MigrateUsersAsync(conn);
        await sut.MigrateTopicsAsync(conn);
        var count = await sut.MigrateIdeasAsync(conn);

        count.Should().Be(4);

        var mongoCount = await GetMongoDb()
            .GetCollection<BsonDocument>("ideas")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        mongoCount.Should().Be(4);
    }

    [Fact]
    public async Task MigrateVotesAsync_TransfersAllVotes()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 1, topicCount: 1, ideaCount: 2, voteCount: 5);

        var sut = CreateSut();
        await sut.MigrateUsersAsync(conn);
        await sut.MigrateTopicsAsync(conn);
        await sut.MigrateIdeasAsync(conn);
        var count = await sut.MigrateVotesAsync(conn);

        count.Should().Be(5);

        var mongoCount = await GetMongoDb()
            .GetCollection<BsonDocument>("votes")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        mongoCount.Should().Be(5);
    }

    [Fact]
    public async Task RunAsync_WithData_MigratesAllEntitiesCorrectly()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 2, topicCount: 2, ideaCount: 3, voteCount: 4);

        var sut    = CreateSut();
        var result = await sut.RunAsync();

        result.MigratedUsers.Should().Be(2);
        result.MigratedTopics.Should().Be(2);
        result.MigratedIdeas.Should().Be(3);
        result.MigratedVotes.Should().Be(4);
        result.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_RunningTwiceProducesSameResult()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 2, topicCount: 2, ideaCount: 3, voteCount: 4);

        var sut = CreateSut();

        // Primera ejecución
        var firstResult = await sut.RunAsync();

        // Segunda ejecución — mismos datos, upsert no debe duplicar
        var secondResult = await sut.RunAsync();

        secondResult.IsConsistent.Should().BeTrue();

        var db = GetMongoDb();
        var mongoUsers  = await db.GetCollection<BsonDocument>("users").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoTopics = await db.GetCollection<BsonDocument>("topics").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoIdeas  = await db.GetCollection<BsonDocument>("ideas").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoVotes  = await db.GetCollection<BsonDocument>("votes").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        mongoUsers.Should().Be(2,  "idempotent: no duplicate users");
        mongoTopics.Should().Be(2, "idempotent: no duplicate topics");
        mongoIdeas.Should().Be(3,  "idempotent: no duplicate ideas");
        mongoVotes.Should().Be(4,  "idempotent: no duplicate votes");
    }

    [Fact]
    public async Task ValidateAsync_WhenCountsMatch_ReturnsConsistentResult()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 2, topicCount: 2, ideaCount: 3, voteCount: 4);

        var sut = CreateSut();
        await sut.RunAsync();

        var validation = await sut.ValidateAsync(conn);

        validation.IsConsistent.Should().BeTrue();
        validation.Users.IsMatch.Should().BeTrue();
        validation.Topics.IsMatch.Should().BeTrue();
        validation.Ideas.IsMatch.Should().BeTrue();
        validation.Votes.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenMongoPendingMigration_ReturnsInconsistentResult()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();
        await SeedMySqlAsync(conn, userCount: 2, topicCount: 2, ideaCount: 3, voteCount: 4);

        // No ejecutamos la migración — MongoDB está vacío
        var sut        = CreateSut();
        var validation = await sut.ValidateAsync(conn);

        validation.IsConsistent.Should().BeFalse();
        validation.Users.MySql.Should().Be(2);
        validation.Users.Mongo.Should().Be(0);
    }

    [Fact]
    public async Task MigrateTopicsAsync_PreservesDescription_WhenNull()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        // Usuario requerido por FK
        var userId = Guid.NewGuid().ToString();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, login, name, password) VALUES (@id, @login, @name, @pwd)",
            new { id = userId, login = "nulldesc", name = "Test", pwd = "x" });

        // Topic con description NULL
        var topicId = Guid.NewGuid().ToString();
        var now     = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "INSERT INTO topics (id, title, description, status, owner_id, created_at, updated_at) VALUES (@id, @title, NULL, 'OPEN', @oid, @now, @now)",
            new { id = topicId, title = "No desc", oid = userId, now });

        var sut = CreateSut();
        await sut.MigrateUsersAsync(conn);
        await sut.MigrateTopicsAsync(conn);

        var doc = await GetMongoDb()
            .GetCollection<BsonDocument>("topics")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", topicId))
            .FirstOrDefaultAsync();

        doc.Should().NotBeNull();
        doc["Description"].BsonType.Should().Be(BsonType.Null,
            "description NULL en MySQL debe mapearse a BsonNull en MongoDB");
    }

    [Fact]
    public async Task MigrateIdeasAsync_PreservesContentJson()
    {
        await using var conn = new MySqlConnection(MysqlConnStr);
        await conn.OpenAsync();

        var userId  = Guid.NewGuid().ToString();
        var topicId = Guid.NewGuid().ToString();
        var ideaId  = Guid.NewGuid().ToString();
        var now     = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var content = "{\"title\":\"My Idea\",\"description\":\"My Desc\",\"isWinning\":true}";

        await conn.ExecuteAsync(
            "INSERT INTO users (id, login, name, password) VALUES (@id, @login, @name, @pwd)",
            new { id = userId, login = "jsontest", name = "Test", pwd = "x" });

        await conn.ExecuteAsync(
            "INSERT INTO topics (id, title, description, status, owner_id, created_at, updated_at) VALUES (@id, 'T', NULL, 'OPEN', @oid, @now, @now)",
            new { id = topicId, oid = userId, now });

        await conn.ExecuteAsync(
            "INSERT INTO ideas (id, topic_id, owner_id, content, created_at, updated_at) VALUES (@id, @tid, @oid, @content, @now, @now)",
            new { id = ideaId, tid = topicId, oid = userId, content, now });

        var sut = CreateSut();
        await sut.MigrateUsersAsync(conn);
        await sut.MigrateTopicsAsync(conn);
        await sut.MigrateIdeasAsync(conn);

        var doc = await GetMongoDb()
            .GetCollection<BsonDocument>("ideas")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ideaId))
            .FirstOrDefaultAsync();

        doc.Should().NotBeNull();
        doc["Content"].AsString.Should().Be(content,
            "el JSON del content debe preservarse exactamente para que el modelo C# lo hidrate igual que en v1");
    }
}