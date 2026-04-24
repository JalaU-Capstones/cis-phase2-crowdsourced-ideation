using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public interface IUserRepository
{
    Task<UserRecord?> GetByIdAsync(string id);
    Task<UserRecord?> GetByLoginAsync(string login);
    Task<IEnumerable<UserRecord>> GetAllAsync();
    Task AddAsync(UserRecord user);
    Task UpdateAsync(UserRecord user);
    Task DeleteAsync(UserRecord user);
    Task<bool> ExistsAsync(string id);
    Task<int> CountAsync();
}
