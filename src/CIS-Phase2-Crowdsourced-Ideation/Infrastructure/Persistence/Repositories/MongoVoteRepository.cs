using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Driver;
using System.Linq;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class MongoVoteRepository(MongoDbContext context) : IVoteRepository
{
    private readonly IMongoCollection<Vote> _collection = context.Votes;

    public async Task<Vote?> GetByIdAsync(Guid id)
    {
        var cursor = await _collection.FindAsync(v => v.Id == id);
        return await FirstOrDefaultAsync(cursor);
    }

    public async Task<IEnumerable<Vote>> GetAllAsync()
    {
        var cursor = await _collection.FindAsync(_ => true);
        return await ToListAsync(cursor);
    }

    public async Task<IEnumerable<Vote>> GetByIdeaIdAsync(Guid ideaId)
    {
        var cursor = await _collection.FindAsync(v => v.IdeaId == ideaId);
        return await ToListAsync(cursor);
    }

    public async Task AddAsync(Vote vote)
    {
        await _collection.InsertOneAsync(vote);
    }

    public async Task UpdateAsync(Vote vote)
    {
        await _collection.ReplaceOneAsync(v => v.Id == vote.Id, vote);
    }

    public async Task DeleteAsync(Vote vote)
    {
        await _collection.DeleteOneAsync(v => v.Id == vote.Id);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
      return await _collection.CountDocumentsAsync(
         Builders<Vote>.Filter.Eq(v => v.Id, id)) > 0;
    }

    public async Task<int> CountByIdeaIdAsync(Guid ideaId)
    {
        return (int)await _collection.CountDocumentsAsync(v => v.IdeaId == ideaId);
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
