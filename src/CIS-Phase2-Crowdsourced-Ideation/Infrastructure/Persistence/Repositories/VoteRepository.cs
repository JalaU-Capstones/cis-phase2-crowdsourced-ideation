using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class VoteRepository(AppDbContext context) : IVoteRepository
{
    public async Task<Vote?> GetByIdAsync(Guid id)
    {
        return await context.Votes.FindAsync(id);
    }

    public async Task<IEnumerable<Vote>> GetAllAsync()
    {
        return await context.Votes.AsNoTracking().ToListAsync();
    }

    public async Task<IEnumerable<Vote>> GetByIdeaIdAsync(Guid ideaId)
    {
        return await context.Votes.AsNoTracking().Where(v => v.IdeaId == ideaId).ToListAsync();
    }

    public async Task AddAsync(Vote vote)
    {
        context.Votes.Add(vote);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Vote vote)
    {
        context.Votes.Remove(vote);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await context.Votes.AnyAsync(v => v.Id == id);
    }

    public async Task<int> CountByIdeaIdAsync(Guid ideaId)
    {
        return await context.Votes.CountAsync(v => v.IdeaId == ideaId);
    }

    public async Task<int> CountAsync()
    {
        return await context.Votes.CountAsync();
    }
}
