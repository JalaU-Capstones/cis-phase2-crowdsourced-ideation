using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Driver;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

public class MongoUserRepository(MongoDbContext context) : IUserRepository
{
    private readonly IMongoCollection<UserRecord> _collection = context.Users;

    public async Task<UserRecord?> GetByIdAsync(string id)
    {
        return await _collection.Find(u => u.Id == id).FirstOrDefaultAsync();
    }

    public async Task<UserRecord?> GetByLoginAsync(string login)
    {
        return await _collection.Find(u => u.Login == login).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<UserRecord>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task AddAsync(UserRecord user)
    {
        await _collection.InsertOneAsync(user);
    }

    public async Task UpdateAsync(UserRecord user)
    {
        await _collection.ReplaceOneAsync(u => u.Id == user.Id, user);
    }

    public async Task DeleteAsync(UserRecord user)
    {
        await _collection.DeleteOneAsync(u => u.Id == user.Id);
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _collection.Find(u => u.Id == id).AnyAsync();
    }

    public async Task<int> CountAsync()
    {
        return (int)await _collection.CountDocumentsAsync(_ => true);
    }
}
