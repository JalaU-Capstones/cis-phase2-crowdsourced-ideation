using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Services;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Shared;

public static class UserIdentityResolver
{
    /// <summary>
    /// Resolves the authenticated user's GUID from the JWT and ensures a corresponding
    /// <see cref="UserRecord"/> exists in the current persistence store.
    ///
    /// This is required because Phase 2 enforces ownership with FK constraints in V1 (MySQL),
    /// and V2 (MongoDB) still needs a stable owner identifier for resources.
    /// </summary>
    public static async Task<Guid> ResolveOrProvisionUserIdAsync(IRepositoryAdapter adapter, ClaimsPrincipal user)
    {
        // NOTE: Phase 2 uses dual persistence. For V2 (MongoDB), we must treat the JWT-provided
        // user identifier as the source of truth to avoid provisioning duplicate "local" users.
        // For V1 (MySQL), we keep the legacy behavior that can resolve users by login and
        // provision a new GUID when the token doesn't contain a GUID id claim.
        if (adapter is MongoDbAdapter)
        {
            return await ResolveOrProvisionMongoUserIdAsync(adapter, user);
        }

        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var login = user.FindFirstValue("login") ?? user.FindFirstValue("preferred_username") ?? sub;
        var name = user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name) ?? login;

        if (string.IsNullOrWhiteSpace(sub) && string.IsNullOrWhiteSpace(login))
            throw new UnauthorizedAccessException("User identity not found or invalid");

        if (!TryGetUserId(user, sub, out var userId))
        {
            var normalizedLogin = NormalizeLogin(login ?? string.Empty);
            var dbUser = await adapter.Users.GetByLoginAsync(normalizedLogin);
            if (dbUser is not null && Guid.TryParse(dbUser.Id, out userId))
                return userId;

            userId = Guid.NewGuid();
        }

        await EnsureUserExistsAsync(adapter, userId, login, name);
        return userId;
    }

    private static async Task<Guid> ResolveOrProvisionMongoUserIdAsync(IRepositoryAdapter adapter, ClaimsPrincipal user)
    {
        // V2 (MongoDB): Phase 1 tokens contain `sub` = login, with no external UUID claim.
        // We resolve the external UUID by:
        // 1) Using an existing Mongo user record keyed by login (already synced).
        // 2) If missing, calling the Java User Management API to fetch the user's UUID, then
        //    persisting it locally (Mongo) and returning that UUID.
        var login =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.Identity?.Name;

        var name = user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name) ?? login;

        if (string.IsNullOrWhiteSpace(login))
            throw new UnauthorizedAccessException("User identity not found or invalid");

        login = login.Trim();

        // First: local Mongo lookup by login.
        var dbUser = await adapter.Users.GetByLoginAsync(login);
        if (dbUser is not null && Guid.TryParse(dbUser.Id, out var existingUserId))
        {
            return existingUserId;
        }

        // Not found locally: fetch from Java API and cache in Mongo.
        var resolver = UserResolverAccessor.Current;
        if (resolver is null)
            throw new UnauthorizedAccessException("External user resolver not configured");

        var external = await resolver.GetByLoginAsync(login);
        if (!Guid.TryParse(external.Id, out var externalUserId))
            throw new UnauthorizedAccessException("External user identity not found or invalid");

        await EnsureMongoUserExistsAsync(adapter, externalUserId, external.Login ?? login, external.Name ?? name);
        return externalUserId;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, string? sub, out Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out userId))
            return true;

        var idClaim = user.FindFirstValue("userId") ?? user.FindFirstValue("id");
        if (!string.IsNullOrWhiteSpace(idClaim) && Guid.TryParse(idClaim, out userId))
            return true;

        userId = default;
        return false;
    }

    private static async Task EnsureUserExistsAsync(IRepositoryAdapter adapter, Guid userId, string? login, string? name)
    {
        var id = userId.ToString();
        if (await adapter.Users.ExistsAsync(id))
            return;

        var normalizedLogin = NormalizeLogin(login ?? id);
        var normalizedName = (name ?? normalizedLogin).Trim();

        // Password is not used by Phase 2; authentication is delegated to Phase 1 (JWT).
        var user = new UserRecord
        {
            Id = id,
            Login = normalizedLogin,
            Name = normalizedName,
            Password = "external"
        };

        await adapter.Users.AddAsync(user);
        await adapter.SaveChangesAsync();
    }

    private static async Task EnsureMongoUserExistsAsync(IRepositoryAdapter adapter, Guid userId, string? login, string? name)
    {
        var id = userId.ToString();
        if (await adapter.Users.ExistsAsync(id))
            return;

        var normalizedLogin = (login ?? id).Trim();
        if (string.IsNullOrWhiteSpace(normalizedLogin))
            normalizedLogin = "unknown";

        var normalizedName = (name ?? normalizedLogin).Trim();

        var user = new UserRecord
        {
            Id = id,
            Login = normalizedLogin,
            Name = normalizedName,
            Password = "external"
        };

        await adapter.Users.AddAsync(user);
        await adapter.SaveChangesAsync();
    }

    private static string NormalizeLogin(string login)
    {
        var v = (login ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v))
            return "unknown";

        // Legacy schema: users.login is VARCHAR(20).
        return v.Length <= 20 ? v : v[..20];
    }
}
