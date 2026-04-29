using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Models;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.Routing;

/// <summary>
/// Repository adapter that routes calls to the appropriate underlying adapter based on the current request
/// and the <see cref="IDatabaseFallbackService"/> decision.
/// </summary>
public sealed class FallbackAdapter(
    [FromKeyedServices("mysql")] IRepositoryAdapter mySql,
    [FromKeyedServices("mongo")] IRepositoryAdapter mongo,
    IDatabaseFallbackService fallback,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FallbackAdapter> logger) : IRepositoryAdapter
{
    public ITopicRepository Topics => GetActive().Topics;
    public IIdeaRepository Ideas => GetActive().Ideas;
    public IVoteRepository Votes => GetActive().Votes;
    public IUserRepository Users => GetActive().Users;

    public Task SaveChangesAsync() => GetActive().SaveChangesAsync();

    private IRepositoryAdapter GetActive()
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        var active = fallback.GetActiveDatabase(path);
        logger.LogWarning("Fallback adapter routing request path {Path} to {Database}.", path, active);

        return active switch
        {
            DatabaseType.MySql => mySql,
            DatabaseType.MongoDb => mongo,
            DatabaseType.BothDown => ThrowBothDown(path),
            _ => mySql
        };
    }

    private IRepositoryAdapter ThrowBothDown(string path)
    {
        // Middleware should intercept this. This is a safety net to avoid silently using the wrong database.
        logger.LogWarning("Both databases are down; repository access attempted for path {Path}.", path);
        throw new InvalidOperationException("Both databases are down.");
    }
}
