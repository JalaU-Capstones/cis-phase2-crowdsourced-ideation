using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Driver;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class MongoIdeaRepository(MongoDbContext context) : IIdeaRepository
{
    private readonly IMongoCollection<Idea> _collection = context.Ideas;

    public async Task<Idea?> GetByIdAsync(Guid id)
    {
        return await _collection.Find(i => i.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Idea>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<IEnumerable<Idea>> GetByTopicIdAsync(string topicId)
    {
        return await _collection.Find(i => i.TopicId == topicId).ToListAsync();
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
        return await _collection.Find(i => i.Id == id).AnyAsync();
    }

    public async Task<int> CountAsync()
    {
        return (int)await _collection.CountDocumentsAsync(_ => true);
    }
}
