using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Driver;
using System.Linq;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class MongoIdeaRepository(MongoDbContext context) : IIdeaRepository
{
    private readonly IMongoCollection<Idea> _collection = context.Ideas;

    public async Task<Idea?> GetByIdAsync(Guid id)
    {
        var cursor = await _collection.FindAsync(i => i.Id == id);
        return await FirstOrDefaultAsync(cursor);
    }

    public async Task<IEnumerable<Idea>> GetAllAsync()
    {
        var cursor = await _collection.FindAsync(_ => true);
        return await ToListAsync(cursor);
    }

    public async Task<IEnumerable<Idea>> GetByTopicIdAsync(string topicId)
    {
        var cursor = await _collection.FindAsync(i => i.TopicId == topicId);
        return await ToListAsync(cursor);
    }

    public async Task AddAsync(Idea idea)
    {
        await _collection.InsertOneAsync(idea);
    }

    public async Task UpdateAsync(Idea idea)
    {
        await _collection.ReplaceOneAsync(i => i.Id == idea.Id, idea);
    }

    public async Task DeleteAsync(Idea idea)
    {
        await _collection.DeleteOneAsync(i => i.Id == idea.Id);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
       return await _collection.CountDocumentsAsync(
         Builders<Idea>.Filter.Eq(i => i.Id, id)) > 0;
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
