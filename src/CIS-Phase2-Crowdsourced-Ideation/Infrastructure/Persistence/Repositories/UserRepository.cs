using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<UserRecord?> GetByIdAsync(string id)
    {
        return await context.Users.FindAsync(id);
    }

    public async Task<UserRecord?> GetByLoginAsync(string login)
    {
        return await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login);
    }

    public async Task<IEnumerable<UserRecord>> GetAllAsync()
    {
        return await context.Users.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(UserRecord user)
    {
        await context.Users.AddAsync(user);
    }

    public async Task UpdateAsync(UserRecord user)
    {
        context.Users.Update(user);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(UserRecord user)
    {
        context.Users.Remove(user);
        await Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await context.Users.AnyAsync(u => u.Id == id);
    }

    public async Task<int> CountAsync()
    {
        return await context.Users.CountAsync();
    }
}
