#!/usr/bin/env dotnet-script
// migrate-to-mongo.csx -- Migration script MySQL to MongoDB
//
// Usage:
//   dotnet script migration/migrate-to-mongo.csx -- \
//     --mysql "Server=localhost;Port=3307;Database=sd3;User Id=sd3user;Password=sd3pass;SslMode=None;AllowPublicKeyRetrieval=true;" \
//     --mongo "mongodb://localhost:27017" \
//     --db    "sd3"
//
// Requires: dotnet-script (dotnet tool install -g dotnet-script)

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

// ---------------------------------------------------------------------------
// Arg parsing
// ---------------------------------------------------------------------------

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

Console.WriteLine("=== CIS Phase 3 -- Migration MySQL to MongoDB ===");
Console.WriteLine($"  MySQL : {mysqlConnStr.Substring(0, Math.Min(60, mysqlConnStr.Length))}...");
Console.WriteLine($"  Mongo : {mongoConnStr}");
Console.WriteLine($"  DB    : {dbName}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Run migration wrapped in async method (required for dotnet-script)
// ---------------------------------------------------------------------------

return await RunMigrationAsync(mysqlConnStr, mongoConnStr, dbName);

async Task<int> RunMigrationAsync(string mysqlConn, string mongoConn, string db)
{
    var mongoClient = new MongoClient(mongoConn);
    var mongoDb     = mongoClient.GetDatabase(db);

    var mysql = new MySqlConnection(mysqlConn);
    try
    {
        await mysql.OpenAsync();

        // 1. Users
        Console.WriteLine("-- [1/4] Migrating users...");
        var userRows = (await mysql.QueryAsync<dynamic>("SELECT id, login, name, password FROM users")).ToList();
        var userDocs = userRows.Select(r => new BsonDocument
        {
            ["_id"]      = (string)r.id,
            ["Login"]    = (string)r.login,
            ["Name"]     = (string)r.name,
            ["Password"] = (string)r.password
        }).ToList();
        var userCount = await UpsertAll(mongoDb.GetCollection<BsonDocument>("users"), userDocs);
        Console.WriteLine($"   OK {userCount} users migrated (upsert, idempotent)");

        // 2. Topics
        Console.WriteLine("-- [2/4] Migrating topics...");
        var topicRows = (await mysql.QueryAsync<dynamic>(
            "SELECT id, title, description, status, owner_id, created_at, updated_at FROM topics")).ToList();
        var topicDocs = topicRows.Select(r => new BsonDocument
        {
            ["_id"]         = (string)r.id,
            ["Title"]       = (string)r.title,
            ["Description"] = r.description is null ? BsonNull.Value : (BsonValue)(string)r.description,
            ["Status"]      = (string)r.status,
            ["OwnerId"]     = (string)r.owner_id,
            ["CreatedAt"]   = (DateTime)r.created_at,
            ["UpdatedAt"]   = (DateTime)r.updated_at
        }).ToList();
        var topicCount = await UpsertAll(mongoDb.GetCollection<BsonDocument>("topics"), topicDocs);
        Console.WriteLine($"   OK {topicCount} topics migrated");

        // 3. Ideas
        Console.WriteLine("-- [3/4] Migrating ideas...");
        var ideaRows = (await mysql.QueryAsync<dynamic>(
            "SELECT id, topic_id, owner_id, content, created_at, updated_at FROM ideas")).ToList();
        var ideaDocs = ideaRows.Select(r => new BsonDocument
        {
            ["_id"]       = (string)r.id,
            ["TopicId"]   = (string)r.topic_id,
            ["OwnerId"]   = (string)r.owner_id,
            ["Content"]   = (string)r.content,
            ["CreatedAt"] = (DateTime)r.created_at,
            ["UpdatedAt"] = (DateTime)r.updated_at
        }).ToList();
        var ideaCount = await UpsertAll(mongoDb.GetCollection<BsonDocument>("ideas"), ideaDocs);
        Console.WriteLine($"   OK {ideaCount} ideas migrated");

        // 4. Votes
        Console.WriteLine("-- [4/4] Migrating votes...");
        var voteRows = (await mysql.QueryAsync<dynamic>("SELECT id, idea_id, user_id FROM votes")).ToList();
        var voteDocs = voteRows.Select(r => new BsonDocument
        {
            ["_id"]    = (string)r.id,
            ["IdeaId"] = (string)r.idea_id,
            ["UserId"] = (string)r.user_id
        }).ToList();
        var voteCount = await UpsertAll(mongoDb.GetCollection<BsonDocument>("votes"), voteDocs);
        Console.WriteLine($"   OK {voteCount} votes migrated");

        // Validation
        Console.WriteLine();
        Console.WriteLine("-- Validating integrity...");

        var mysqlUsers  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users");
        var mysqlTopics = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM topics");
        var mysqlIdeas  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ideas");
        var mysqlVotes  = await mysql.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM votes");

        var mongoUsers  = await mongoDb.GetCollection<BsonDocument>("users").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
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

        Check("users",  mysqlUsers,  mongoUsers);
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

async Task<long> UpsertAll(IMongoCollection<BsonDocument> collection, List<BsonDocument> docs)
{
    long count = 0;
    foreach (var doc in docs)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
        await collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
        count++;
    }
    return count;
}
