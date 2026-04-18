using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public interface ITopicRepository
{
    Task<Topic?> GetByIdAsync(string id);
    Task<IEnumerable<Topic>> GetAllAsync();
    Task AddAsync(Topic topic);
    Task UpdateAsync(Topic topic);
    Task DeleteAsync(Topic topic);
    Task<bool> ExistsAsync(string id);
    Task<IEnumerable<Topic>> GetFilteredAsync(string? status, string? ownerId);
    Task<int> CountAsync();
}
