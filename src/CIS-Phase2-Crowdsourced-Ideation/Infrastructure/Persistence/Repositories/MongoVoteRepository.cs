using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Driver;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class MongoVoteRepository(MongoDbContext context) : IVoteRepository
{
    private readonly IMongoCollection<Vote> _collection = context.Votes;

    public async Task<Vote?> GetByIdAsync(Guid id)
    {
        return await _collection.Find(v => v.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Vote>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<IEnumerable<Vote>> GetByIdeaIdAsync(Guid ideaId)
    {
        return await _collection.Find(v => v.IdeaId == ideaId).ToListAsync();
    }

    public async Task AddAsync(Vote vote)
    {
        await _collection.InsertOneAsync(vote);
    }

    public async Task DeleteAsync(Vote vote)
    {
        await _collection.DeleteOneAsync(v => v.Id == vote.Id);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _collection.Find(v => v.Id == id).AnyAsync();
    }

    public async Task<int> CountByIdeaIdAsync(Guid ideaId)
    {
        return (int)await _collection.CountDocumentsAsync(v => v.IdeaId == ideaId);
    }

    public async Task<int> CountAsync()
    {
        return (int)await _collection.CountDocumentsAsync(_ => true);
    }
}
