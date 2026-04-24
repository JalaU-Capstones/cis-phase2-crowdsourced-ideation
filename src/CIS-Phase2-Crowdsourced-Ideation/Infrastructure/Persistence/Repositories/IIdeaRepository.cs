using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public interface IIdeaRepository
{
    Task<Idea?> GetByIdAsync(Guid id);
    Task<IEnumerable<Idea>> GetAllAsync();
    Task<IEnumerable<Idea>> GetByTopicIdAsync(string topicId);
    Task AddAsync(Idea idea);
    Task UpdateAsync(Idea idea);
    Task DeleteAsync(Idea idea);
    Task<bool> ExistsAsync(Guid id);
    Task<int> CountAsync();
}
