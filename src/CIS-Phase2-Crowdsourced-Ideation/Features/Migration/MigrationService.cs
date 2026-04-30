using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;
using Dapper;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Migration;

/// <summary>
/// Production DI-registered implementation of IMigrationService.
/// Reads connection strings from IConfiguration — no hardcoded values.
/// NEVER writes to the `users` collection (owned exclusively by Java Phase 1).
/// </summary>
public sealed class MigrationService : IMigrationService
{
    private readonly string _mysqlConnectionString;
    private readonly IMongoDatabase _mongoDatabase;

    
    public MigrationService(IConfiguration configuration)
    {
        _mysqlConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured.");

        var mongoConnectionString = configuration.GetConnectionString("MongoDbConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:MongoDbConnection is not configured.");

        var mongoUrl     = MongoUrl.Create(mongoConnectionString);
        var databaseName = mongoUrl.DatabaseName
            ?? throw new InvalidOperationException(
                "MongoDbConnection must include a database name (e.g. mongodb://host/dbname).");

        _mongoDatabase = new MongoClient(mongoUrl).GetDatabase(databaseName);
    }

    /// <summary>
    /// Testability constructor — used by Testcontainers integration tests
    /// and by unit tests that supply mock IMongoDatabase instances.
    /// </summary>
    public MigrationService(string mysqlConnectionString, IMongoDatabase mongoDatabase)
    {
        _mysqlConnectionString = mysqlConnectionString;
        _mongoDatabase         = mongoDatabase;
    }

    public async Task<MigrationResult> RunAsync()
    {
        await using var mysql = new MySqlConnection(_mysqlConnectionString);
        await mysql.OpenAsync();

        var missingUsers = await ValidateMissingUsersAsync(mysql);
        if (missingUsers.Count > 0)
            throw new InvalidOperationException(
                $"Missing users in MongoDB. Please run Phase 1 user migration first. " +
                $"Missing IDs: {string.Join(", ", missingUsers.Take(10))}" +
                (missingUsers.Count > 10 ? $" … and {missingUsers.Count - 10} more." : "."));

        var topics     = await MigrateTopicsAsync(mysql);
        var ideas      = await MigrateIdeasAsync(mysql);
        var votes      = await MigrateVotesAsync(mysql);
        var validation = await ValidateAsync(mysql);

        return new MigrationResult(topics, ideas, votes, validation);
    }


    public async Task<IReadOnlyList<string>> ValidateMissingUsersAsync(MySqlConnection mysql)
    {
        var referencedIds = new HashSet<string>();
        foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT owner_id FROM topics"))
            referencedIds.Add(id);
        foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT owner_id FROM ideas"))
            referencedIds.Add(id);
        foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT user_id  FROM votes"))
            referencedIds.Add(id);

        if (referencedIds.Count == 0)
            return Array.Empty<string>();

        var existing = await _mongoDatabase.GetCollection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.In("_id", referencedIds))
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToListAsync();

        var existingIds = existing.Select(d => d["_id"].AsString).ToHashSet();
        return referencedIds.Where(id => !existingIds.Contains(id)).ToList();
    }

    private async Task<long> MigrateTopicsAsync(MySqlConnection mysql)
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
                    ? BsonNull.Value
                    : (BsonValue)(string)row.description,
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

    private async Task<long> MigrateIdeasAsync(MySqlConnection mysql)
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

    private async Task<long> MigrateVotesAsync(MySqlConnection mysql)
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
        var mTopics = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM topics");
        var mIdeas  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ideas");
        var mVotes  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM votes");

        var getCol  = _mongoDatabase.GetCollection<BsonDocument>;
        var mgTopics = await getCol("topics").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mgIdeas  = await getCol("ideas") .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mgVotes  = await getCol("votes") .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        return new ValidationResult(
            Topics: new CountPair(mTopics, mgTopics),
            Ideas:  new CountPair(mIdeas,  mgIdeas),
            Votes:  new CountPair(mVotes,  mgVotes));
    }
}


public sealed record MigrationResult(
    long MigratedTopics,
    long MigratedIdeas,
    long MigratedVotes,
    ValidationResult Validation)
{
    public bool IsConsistent => Validation.IsConsistent;
}

public sealed record ValidationResult(CountPair Topics, CountPair Ideas, CountPair Votes)
{
    public bool IsConsistent => Topics.IsMatch && Ideas.IsMatch && Votes.IsMatch;
}

public sealed record CountPair(long MySql, long Mongo)
{
    public bool IsMatch => MySql == Mongo;
}