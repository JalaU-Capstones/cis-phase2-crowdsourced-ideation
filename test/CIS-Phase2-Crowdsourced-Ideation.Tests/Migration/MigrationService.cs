using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;
using Dapper;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Migration;

/// <summary>
/// Encapsula la lógica de migración MySQL → MongoDB.
/// Diseñada para ser testeable de forma independiente del script .csx.
/// Usa upsert (ReplaceOneAsync con IsUpsert=true) para garantizar idempotencia.
/// </summary>
public sealed class MigrationService
{
    private readonly string _mysqlConnectionString;
    private readonly IMongoDatabase _mongoDatabase;

    public MigrationService(string mysqlConnectionString, string mongoConnectionString, string databaseName)
    {
        _mysqlConnectionString = mysqlConnectionString;
        var client = new MongoClient(mongoConnectionString);
        _mongoDatabase = client.GetDatabase(databaseName);
    }

    // Constructor alternativo para tests que ya tienen IMongoDatabase
    public MigrationService(string mysqlConnectionString, IMongoDatabase mongoDatabase)
    {
        _mysqlConnectionString = mysqlConnectionString;
        _mongoDatabase = mongoDatabase;
    }

    public async Task<MigrationResult> RunAsync()
    {
        await using var mysql = new MySqlConnection(_mysqlConnectionString);
        await mysql.OpenAsync();

        var users  = await MigrateUsersAsync(mysql);
        var topics = await MigrateTopicsAsync(mysql);
        var ideas  = await MigrateIdeasAsync(mysql);
        var votes  = await MigrateVotesAsync(mysql);

        var validation = await ValidateAsync(mysql);

        return new MigrationResult(users, topics, ideas, votes, validation);
    }

    // ---------------------------------------------------------------------------
    // Migration steps
    // ---------------------------------------------------------------------------

    public async Task<long> MigrateUsersAsync(MySqlConnection mysql)
    {
        var rows = await mysql.QueryAsync<dynamic>(
            "SELECT id, login, name, password FROM users");

        var collection = _mongoDatabase.GetCollection<BsonDocument>("users");
        long count = 0;

        foreach (var row in rows)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", (string)row.id);
            var doc = new BsonDocument
            {
                ["_id"]      = (string)row.id,
                ["Login"]    = (string)row.login,
                ["Name"]     = (string)row.name,
                ["Password"] = (string)row.password
            };
            await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            count++;
        }

        return count;
    }

    public async Task<long> MigrateTopicsAsync(MySqlConnection mysql)
    {
        var rows = await mysql.QueryAsync<dynamic>(
            "SELECT id, title, description, status, owner_id, created_at, updated_at FROM topics");

        var collection = _mongoDatabase.GetCollection<BsonDocument>("topics");
        long count = 0;

        foreach (var row in rows)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", (string)row.id);
            var doc = new BsonDocument
            {
                ["_id"]         = (string)row.id,
                ["Title"]       = (string)row.title,
                ["Description"] = row.description is null ? BsonNull.Value : (BsonValue)(string)row.description,
                ["Status"]      = (string)row.status,
                ["OwnerId"]     = (string)row.owner_id,
                ["CreatedAt"]   = (DateTime)row.created_at,
                ["UpdatedAt"]   = (DateTime)row.updated_at
            };
            await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            count++;
        }

        return count;
    }

    public async Task<long> MigrateIdeasAsync(MySqlConnection mysql)
    {
        var rows = await mysql.QueryAsync<dynamic>(
            "SELECT id, topic_id, owner_id, content, created_at, updated_at FROM ideas");

        var collection = _mongoDatabase.GetCollection<BsonDocument>("ideas");
        long count = 0;

        foreach (var row in rows)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", (string)row.id);
            var doc = new BsonDocument
            {
                ["_id"]       = (string)row.id,
                ["TopicId"]   = (string)row.topic_id,
                ["OwnerId"]   = (string)row.owner_id,
                ["Content"]   = (string)row.content,
                ["CreatedAt"] = (DateTime)row.created_at,
                ["UpdatedAt"] = (DateTime)row.updated_at
            };
            await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            count++;
        }

        return count;
    }

    public async Task<long> MigrateVotesAsync(MySqlConnection mysql)
    {
        var rows = await mysql.QueryAsync<dynamic>(
            "SELECT id, idea_id, user_id FROM votes");

        var collection = _mongoDatabase.GetCollection<BsonDocument>("votes");
        long count = 0;

        foreach (var row in rows)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", (string)row.id);
            var doc = new BsonDocument
            {
                ["_id"]    = (string)row.id,
                ["IdeaId"] = (string)row.idea_id,
                ["UserId"] = (string)row.user_id
            };
            await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
            count++;
        }

        return count;
    }

    // ---------------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------------

    public async Task<ValidationResult> ValidateAsync(MySqlConnection mysql)
    {
        var mysqlUsers  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users");
        var mysqlTopics = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM topics");
        var mysqlIdeas  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ideas");
        var mysqlVotes  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM votes");

        var mongoUsers  = await _mongoDatabase.GetCollection<BsonDocument>("users").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoTopics = await _mongoDatabase.GetCollection<BsonDocument>("topics").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoIdeas  = await _mongoDatabase.GetCollection<BsonDocument>("ideas").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoVotes  = await _mongoDatabase.GetCollection<BsonDocument>("votes").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        return new ValidationResult(
            Users:  new CountPair(mysqlUsers,  mongoUsers),
            Topics: new CountPair(mysqlTopics, mongoTopics),
            Ideas:  new CountPair(mysqlIdeas,  mongoIdeas),
            Votes:  new CountPair(mysqlVotes,  mongoVotes)
        );
    }
}

// ---------------------------------------------------------------------------
// Result types
// ---------------------------------------------------------------------------

public sealed record MigrationResult(
    long MigratedUsers,
    long MigratedTopics,
    long MigratedIdeas,
    long MigratedVotes,
    ValidationResult Validation)
{
    public bool IsConsistent => Validation.IsConsistent;
}

public sealed record ValidationResult(
    CountPair Users,
    CountPair Topics,
    CountPair Ideas,
    CountPair Votes)
{
    public bool IsConsistent =>
        Users.IsMatch && Topics.IsMatch && Ideas.IsMatch && Votes.IsMatch;
}

public sealed record CountPair(long MySql, long Mongo)
{
    public bool IsMatch => MySql == Mongo;
}