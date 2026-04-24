using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Driver;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class MongoTopicRepository(MongoDbContext context) : ITopicRepository
{
    private readonly IMongoCollection<Topic> _collection = context.Topics;

    public async Task<Topic?> GetByIdAsync(string id)
    {
        return await _collection.Find(t => t.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Topic>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task AddAsync(Topic topic)
    {
        await _collection.InsertOneAsync(topic);
    }

    public async Task UpdateAsync(Topic topic)
    {
        await _collection.ReplaceOneAsync(t => t.Id == topic.Id, topic);
    }

    public async Task DeleteAsync(Topic topic)
    {
        await _collection.DeleteOneAsync(t => t.Id == topic.Id);
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _collection.Find(t => t.Id == id).AnyAsync();
    }

    public async Task<IEnumerable<Topic>> GetFilteredAsync(string? status, string? ownerId)
    {
        var filter = Builders<Topic>.Filter.Empty;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TopicStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            filter &= Builders<Topic>.Filter.Eq(t => t.Status, parsedStatus);
        }
        if (!string.IsNullOrEmpty(ownerId))
        {
            filter &= Builders<Topic>.Filter.Eq(t => t.OwnerId, ownerId);
        }
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<int> CountAsync()
    {
        return (int)await _collection.CountDocumentsAsync(_ => true);
    }
}
