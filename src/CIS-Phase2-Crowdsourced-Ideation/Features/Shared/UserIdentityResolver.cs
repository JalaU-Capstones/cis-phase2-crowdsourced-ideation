using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

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
        // V2 (MongoDB): tokens may have `sub` as login (e.g. "jroca") rather than a UUID.
        // To prevent duplicates we:
        // 1) Prefer an explicit UUID claim if present (userId/id/etc).
        // 2) Otherwise lookup by login in Mongo's users collection and reuse that id.
        // 3) Provision a placeholder only if truly missing (first interaction).
        var sub = user.FindFirstValue("sub")
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.Identity?.Name;

        var externalIdClaim = user.FindFirstValue("userId")
                              ?? user.FindFirstValue("id")
                              ?? user.FindFirstValue("uid")
                              ?? user.FindFirstValue("user_id")
                              ?? user.FindFirstValue("jti");

        var login = user.FindFirstValue("login") ?? user.FindFirstValue("preferred_username") ?? sub;
        var name = user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name) ?? login;

        if (!string.IsNullOrWhiteSpace(externalIdClaim) && Guid.TryParse(externalIdClaim, out var userIdFromClaim))
        {
            await EnsureUserExistsAsync(adapter, userIdFromClaim, login, name);
            return userIdFromClaim;
        }

        if (string.IsNullOrWhiteSpace(login))
            throw new UnauthorizedAccessException("User identity not found or invalid");

        var normalizedLogin = NormalizeLogin(login);
        var dbUser = await adapter.Users.GetByLoginAsync(normalizedLogin);
        if (dbUser is not null && Guid.TryParse(dbUser.Id, out var existingUserId))
        {
            // Existing login mapping: reuse id and do not create duplicates.
            return existingUserId;
        }

        // First-time interaction and we don't have an external UUID in the JWT.
        // We provision a placeholder user id to keep ownership stable within this API.
        var provisionedId = Guid.NewGuid();
        await EnsureUserExistsAsync(adapter, provisionedId, normalizedLogin, name);
        return provisionedId;
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

    private static string NormalizeLogin(string login)
    {
        var v = (login ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v))
            return "unknown";

        // Legacy schema: users.login is VARCHAR(20).
        return v.Length <= 20 ? v : v[..20];
    }
}
