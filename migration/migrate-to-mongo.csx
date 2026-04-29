#!/usr/bin/env dotnet-script
// migrate-to-mongo.csx -- Migration script MySQL to MongoDB (Phase 2 only)
//
// IMPORTANT: Run Phase 1 Java user migration BEFORE this script.
//            This script migrates: topics, ideas, votes.
//            It NEVER writes to the `users` collection.
//
// Usage:
//   dotnet script migration/migrate-to-mongo.csx -- \
//     --mysql "Server=localhost;Port=3307;Database=sd3;User Id=sd3user;Password=sd3pass;SslMode=None;AllowPublicKeyRetrieval=true;" \
//     --mongo "mongodb://localhost:27017" \
//     --db    "sd3"

#r "nuget: MySqlConnector, 2.3.7"
#r "nuget: MongoDB.Driver, 3.0.0"
#r "nuget: Dapper, 2.1.35"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;

var argList = Args.ToList();

string GetArg(string name)
{
    var idx = argList.IndexOf(name);
    if (idx < 0 || idx + 1 >= argList.Count)
        throw new ArgumentException($"Required argument missing: {name}");
    return argList[idx + 1];
}

var mysqlConnStr = GetArg("--mysql");
var mongoConnStr = GetArg("--mongo");
var dbName       = GetArg("--db");

Console.WriteLine("=== CIS Phase 2 -- Migration MySQL to MongoDB ===");
Console.WriteLine($"  MySQL : {mysqlConnStr.Substring(0, Math.Min(60, mysqlConnStr.Length))}...");
Console.WriteLine($"  Mongo : {mongoConnStr}");
Console.WriteLine($"  DB    : {dbName}");
Console.WriteLine($"  Scope : topics, ideas, votes (users owned by Java Phase 1)");
Console.WriteLine();

return await RunMigrationAsync(mysqlConnStr, mongoConnStr, dbName);

async Task<int> RunMigrationAsync(string mysqlConn, string mongoConn, string db)
{
    var mongoClient = new MongoClient(mongoConn);
    var mongoDb     = mongoClient.GetDatabase(db);

    var mysql = new MySqlConnection(mysqlConn);
    try
    {
        await mysql.OpenAsync();

        // Pre-validation: all referenced user IDs must exist in MongoDB (from Phase 1)
        Console.WriteLine("-- [0/4] Validating Phase 1 users in MongoDB...");
        var missing = await GetMissingUserIdsAsync(mysql, mongoDb);
        if (missing.Count > 0)
        {
            Console.WriteLine($"   ERROR: {missing.Count} user ID(s) referenced in MySQL are missing from MongoDB.");
            Console.WriteLine($"   Missing IDs: {string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? " ..." : "")}");
            Console.WriteLine();
            Console.WriteLine("   Please run Phase 1 Java user migration first, then retry.");
            return 1;
        }
        Console.WriteLine("   OK - all referenced users exist in MongoDB");

        // 1. Topics
        Console.WriteLine("-- [1/3] Migrating topics...");
        var topicCount = await UpsertFromMySqlAsync(mysql, mongoDb, "topics",
            "SELECT id, title, description, status, owner_id, created_at, updated_at FROM topics",
            row => new BsonDocument
            {
                ["_id"]         = (string)row.id,
                ["Title"]       = (string)row.title,
                ["Description"] = row.description is null ? BsonNull.Value : (BsonValue)(string)row.description,
                ["Status"]      = (string)row.status,
                ["OwnerId"]     = (string)row.owner_id,
                ["CreatedAt"]   = (DateTime)row.created_at,
                ["UpdatedAt"]   = (DateTime)row.updated_at
            });
        Console.WriteLine($"   OK {topicCount} topics migrated");

        // 2. Ideas
        Console.WriteLine("-- [2/3] Migrating ideas...");
        var ideaCount = await UpsertFromMySqlAsync(mysql, mongoDb, "ideas",
            "SELECT id, topic_id, owner_id, content, created_at, updated_at FROM ideas",
            row => new BsonDocument
            {
                ["_id"]       = (string)row.id,
                ["TopicId"]   = (string)row.topic_id,
                ["OwnerId"]   = (string)row.owner_id,
                ["Content"]   = (string)row.content,
                ["CreatedAt"] = (DateTime)row.created_at,
                ["UpdatedAt"] = (DateTime)row.updated_at
            });
        Console.WriteLine($"   OK {ideaCount} ideas migrated");

        // 3. Votes
        Console.WriteLine("-- [3/3] Migrating votes...");
        var voteCount = await UpsertFromMySqlAsync(mysql, mongoDb, "votes",
            "SELECT id, idea_id, user_id FROM votes",
            row => new BsonDocument
            {
                ["_id"]    = (string)row.id,
                ["IdeaId"] = (string)row.idea_id,
                ["UserId"] = (string)row.user_id
            });
        Console.WriteLine($"   OK {voteCount} votes migrated");

        // Validation
        Console.WriteLine();
        Console.WriteLine("-- Validating integrity...");

        var mysqlTopics = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM topics");
        var mysqlIdeas  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ideas");
        var mysqlVotes  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM votes");

        var mongoTopics = await mongoDb.GetCollection<BsonDocument>("topics").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoIdeas  = await mongoDb.GetCollection<BsonDocument>("ideas").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        var mongoVotes  = await mongoDb.GetCollection<BsonDocument>("votes").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        bool ok = true;

        void Check(string entity, long sqlCount, long mongoCount)
        {
            var status = sqlCount == mongoCount ? "OK" : "MISMATCH";
            Console.WriteLine($"   {status,-8} {entity,-10} MySQL={sqlCount,6}  MongoDB={mongoCount,6}");
            if (sqlCount != mongoCount) ok = false;
        }

        Check("topics", mysqlTopics, mongoTopics);
        Check("ideas",  mysqlIdeas,  mongoIdeas);
        Check("votes",  mysqlVotes,  mongoVotes);

        Console.WriteLine();
        if (ok)
        {
            Console.WriteLine("Migration completed. 100% data consistency verified.");
            return 0;
        }
        else
        {
            Console.WriteLine("Migration has inconsistencies. Check the output above.");
            return 1;
        }
    }
    finally
    {
        mysql.Dispose();
    }
}

async Task<List<string>> GetMissingUserIdsAsync(MySqlConnection mysql, IMongoDatabase mongoDb)
{
    var referencedIds = new HashSet<string>();
    foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT owner_id FROM topics")) referencedIds.Add(id);
    foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT owner_id FROM ideas"))  referencedIds.Add(id);
    foreach (var id in await mysql.QueryAsync<string>("SELECT DISTINCT user_id FROM votes"))   referencedIds.Add(id);

    if (referencedIds.Count == 0) return new List<string>();

    var filter   = Builders<BsonDocument>.Filter.In("_id", referencedIds);
    var existing = await mongoDb.GetCollection<BsonDocument>("users")
        .Find(filter).Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync();
    var existingIds = existing.Select(d => d["_id"].AsString).ToHashSet();

    return referencedIds.Where(id => !existingIds.Contains(id)).ToList();
}

async Task<long> UpsertFromMySqlAsync(
    MySqlConnection mysql,
    IMongoDatabase mongoDb,
    string collectionName,
    string query,
    Func<dynamic, BsonDocument> mapRow)
{
    var rows       = (await mysql.QueryAsync<dynamic>(query)).ToList();
    var collection = mongoDb.GetCollection<BsonDocument>(collectionName);
    long count     = 0;

    foreach (var row in rows)
    {
        var doc    = mapRow(row);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
        await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
        count++;
    }
    return count;
}
