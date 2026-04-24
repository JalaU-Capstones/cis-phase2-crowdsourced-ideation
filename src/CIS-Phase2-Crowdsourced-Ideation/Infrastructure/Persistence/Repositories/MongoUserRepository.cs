using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using MongoDB.Bson;
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
        // Existing Mongo datasets may vary in field casing (`Login` vs `login`) and `_id` representation.
        // Avoid binding/deserialization issues by projecting to BsonDocument and mapping manually.
        var filter = Builders<UserRecord>.Filter.Or(
            Builders<UserRecord>.Filter.Eq("Login", login),
            Builders<UserRecord>.Filter.Eq("login", login));

        var projection = Builders<UserRecord>.Projection
            .Include("_id")
            .Include("Login").Include("login")
            .Include("Name").Include("name")
            .Include("Password").Include("password");

        var doc = await _collection
            .Find(filter)
            .Project<BsonDocument>(projection)
            .FirstOrDefaultAsync();

        if (doc is null)
            return null;

        var id = ReadId(doc);
        var mappedLogin = ReadString(doc, "Login") ?? ReadString(doc, "login") ?? string.Empty;
        var mappedName = ReadString(doc, "Name") ?? ReadString(doc, "name") ?? mappedLogin;
        var mappedPassword = ReadString(doc, "Password") ?? ReadString(doc, "password") ?? "external";

        return new UserRecord
        {
            Id = id,
            Login = mappedLogin,
            Name = mappedName,
            Password = mappedPassword
        };
    }

    private static string ReadId(BsonDocument doc)
    {
        if (!doc.TryGetValue("_id", out var v) || v is null)
            return string.Empty;

        if (v.IsString) return v.AsString;
        if (v.IsGuid) return v.AsGuid.ToString();
        if (v.IsObjectId) return v.AsObjectId.ToString();

        // Fallback for unusual id representations.
        return v.ToString() ?? string.Empty;
    }

    private static string? ReadString(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v is null)
            return null;

        return v.IsString ? v.AsString : v.ToString();
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
