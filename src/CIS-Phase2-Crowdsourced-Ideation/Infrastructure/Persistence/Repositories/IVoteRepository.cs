using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public interface IVoteRepository
{
    Task<Vote?> GetByIdAsync(Guid id);
    Task<IEnumerable<Vote>> GetAllAsync();
    Task<IEnumerable<Vote>> GetByIdeaIdAsync(Guid ideaId);
    Task AddAsync(Vote vote);
    Task DeleteAsync(Vote vote);
    Task<bool> ExistsAsync(Guid id);
    Task<int> CountByIdeaIdAsync(Guid ideaId);
    Task<int> CountAsync();
}
