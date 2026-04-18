using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class TopicRepository(AppDbContext context) : ITopicRepository
{
    public async Task<Topic?> GetByIdAsync(string id)
    {
        return await context.Topics.FindAsync(id);
    }

    public async Task<IEnumerable<Topic>> GetAllAsync()
    {
        return await context.Topics.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(Topic topic)
    {
        context.Topics.Add(topic);
    }

    public async Task UpdateAsync(Topic topic)
    {
        context.Topics.Update(topic);
    }

    public async Task DeleteAsync(Topic topic)
    {
        context.Entry(topic).State = EntityState.Deleted;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await context.Topics.AnyAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Topic>> GetFilteredAsync(string? status, string? ownerId)
    {
        var query = context.Topics.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TopicStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }
        if (!string.IsNullOrEmpty(ownerId))
        {
            query = query.Where(t => t.OwnerId == ownerId);
        }
        return await query.ToListAsync();
    }

    public async Task<int> CountAsync()
    {
        return await context.Topics.CountAsync();
    }
}
