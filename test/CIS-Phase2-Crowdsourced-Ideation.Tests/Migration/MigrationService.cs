using CIS.Phase2.CrowdsourcedIdeation.Features.Migration;
using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;
using Dapper;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Migration;

/// <summary>
/// Concrete implementation used exclusively by Testcontainers integration tests
/// Uses the two-argument constructor so tests
/// can supply real Testcontainers-managed databases without touching appsettings.
/// </summary>
public sealed class MigrationService : IMigrationService
{
    private readonly string _mysqlConnectionString;
    private readonly IMongoDatabase _mongoDatabase;

    public MigrationService(
        string mysqlConnectionString,
        string mongoConnectionString,
        string databaseName)
    {
        _mysqlConnectionString = mysqlConnectionString;
        _mongoDatabase = new MongoClient(mongoConnectionString).GetDatabase(databaseName);
    }

    public MigrationService(string mysqlConnectionString, IMongoDatabase mongoDatabase)
    {
        _mysqlConnectionString = mysqlConnectionString;
        _mongoDatabase         = mongoDatabase;
    }

    public async Task<MigrationResult> RunAsync()
    {
        var mysql = new MySqlConnection(_mysqlConnectionString);
        try
        {
            await mysql.OpenAsync();

            var missingUsers = await ValidateMissingUsersAsync(mysql);
            if (missingUsers.Count > 0)
                throw new InvalidOperationException(
                    $"Missing users in MongoDB. Please run Phase 1 user migration first. " +
                    $"Missing IDs: {string.Join(", ", missingUsers.Take(10))}" +
                    (missingUsers.Count > 10 ? $" ... and {missingUsers.Count - 10} more." : "."));

            var topics     = await MigrateTopicsAsync(mysql);
            var ideas      = await MigrateIdeasAsync(mysql);
            var votes      = await MigrateVotesAsync(mysql);
            var validation = await ValidateAsync(mysql);

            return new MigrationResult(topics, ideas, votes, validation);
        }
        finally
        {
            mysql.Dispose();
        }
    }
    

    public async Task<IReadOnlyList<string>> ValidateMissingUsersAsync(MySqlConnection mysql)
    {
        var referencedIds = new HashSet<string>();
        foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT owner_id FROM topics"))
            referencedIds.Add(id);
        foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT owner_id FROM ideas"))
            referencedIds.Add(id);
        foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT user_id FROM votes"))
            referencedIds.Add(id);

        if (referencedIds.Count == 0)
            return Array.Empty<string>();

        var usersCollection = _mongoDatabase.GetCollection<BsonDocument>("users");
        var filter          = Builders<BsonDocument>.Filter.In("_id", referencedIds);
        var existingDocs    = await usersCollection.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToListAsync();

        var existingIds = existingDocs.Select(d => d["_id"].AsString).ToHashSet();
        return referencedIds.Where(id => !existingIds.Contains(id)).ToList();
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
                ["Description"] = row.description is null
                    ? BsonNull.Value : (BsonValue)(string)row.description,
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
        var rows = await mysql.QueryAsync<dynamic>("SELECT id, idea_id, user_id FROM votes");
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

    public async Task<ValidationResult> ValidateAsync(MySqlConnection mysql)
    {
        var mysqlTopics = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM topics");
        var mysqlIdeas  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ideas");
        var mysqlVotes  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM votes");

        var mongoTopics = await _mongoDatabase.GetCollection<BsonDocument>("topics")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoIdeas  = await _mongoDatabase.GetCollection<BsonDocument>("ideas")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoVotes  = await _mongoDatabase.GetCollection<BsonDocument>("votes")
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        return new ValidationResult(
            Topics: new CountPair(mysqlTopics, mongoTopics),
            Ideas:  new CountPair(mysqlIdeas,  mongoIdeas),
            Votes:  new CountPair(mysqlVotes,  mongoVotes));
    }
}