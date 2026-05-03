using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

internal sealed class MongoClientFactory(IConfiguration configuration) : IMongoClientFactory
{
    public IMongoClient Create()
    {
        var cs = configuration.GetConnectionString("MongoDbConnection") ?? "mongodb://localhost:27017/sd3";
        var settings = MongoClientSettings.FromConnectionString(cs);
        // Ensure probes fail fast when MongoDB is down.
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(1);
        settings.ConnectTimeout = TimeSpan.FromSeconds(1);
        return new MongoClient(settings);
    }
}
