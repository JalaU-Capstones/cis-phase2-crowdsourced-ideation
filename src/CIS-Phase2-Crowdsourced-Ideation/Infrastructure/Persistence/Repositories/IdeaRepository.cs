using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class IdeaRepository(AppDbContext context) : IIdeaRepository
{
    public async Task<Idea?> GetByIdAsync(Guid id)
    {
        return await context.Ideas.FindAsync(id);
    }

    public async Task<IEnumerable<Idea>> GetAllAsync()
    {
        return await context.Ideas.AsNoTracking().ToListAsync();
    }

    public async Task<IEnumerable<Idea>> GetByTopicIdAsync(string topicId)
    {
        return await context.Ideas.AsNoTracking().Where(i => i.TopicId == topicId).ToListAsync();
    }

    public async Task AddAsync(Idea idea)
    {
        context.Ideas.Add(idea);
    }

    public async Task UpdateAsync(Idea idea)
    {
        context.Ideas.Update(idea);
    }

    public async Task DeleteAsync(Idea idea)
    {
        var existing = await context.Ideas.FindAsync(idea.Id);
        if (existing != null)
            context.Ideas.Remove(existing);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Ideas.AnyAsync(i => i.Id == id);
    }

    public async Task<int> CountAsync()
    {
        return await context.Ideas.CountAsync();
    }
}
