using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using MongoDB.Driver;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<Topic> Topics => _database.GetCollection<Topic>("topics");
    public IMongoCollection<Idea> Ideas => _database.GetCollection<Idea>("ideas");
    public IMongoCollection<Vote> Votes => _database.GetCollection<Vote>("votes");
    public IMongoCollection<UserRecord> Users => _database.GetCollection<UserRecord>("users");
}
