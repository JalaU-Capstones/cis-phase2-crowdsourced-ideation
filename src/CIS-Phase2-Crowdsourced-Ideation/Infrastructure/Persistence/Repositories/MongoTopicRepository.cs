using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Driver;
using System.Linq;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class MongoTopicRepository(MongoDbContext context) : ITopicRepository
{
    private readonly IMongoCollection<Topic> _collection = context.Topics;

    public async Task<Topic?> GetByIdAsync(string id)
    {
        var cursor = await _collection.FindAsync(t => t.Id == id);
        return await FirstOrDefaultAsync(cursor);
    }

    public async Task<IEnumerable<Topic>> GetAllAsync()
    {
        var cursor = await _collection.FindAsync(_ => true);
        return await ToListAsync(cursor);
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
       return await _collection.CountDocumentsAsync(
           Builders<Topic>.Filter.Eq(t => t.Id, id)) > 0;
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
        var cursor = await _collection.FindAsync(filter);
        return await ToListAsync(cursor);
    }

    public async Task<int> CountAsync()
    {
        return (int)await _collection.CountDocumentsAsync(_ => true);
    }

    private static async Task<T?> FirstOrDefaultAsync<T>(IAsyncCursor<T> cursor, CancellationToken ct = default)
    {
        if (await cursor.MoveNextAsync(ct) && cursor.Current != null)
            return cursor.Current.FirstOrDefault();
        return default;
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncCursor<T> cursor, CancellationToken ct = default)
    {
        var items = new List<T>();
        while (await cursor.MoveNextAsync(ct))
        {
            if (cursor.Current is null) continue;
            items.AddRange(cursor.Current);
        }
        return items;
    }
}
