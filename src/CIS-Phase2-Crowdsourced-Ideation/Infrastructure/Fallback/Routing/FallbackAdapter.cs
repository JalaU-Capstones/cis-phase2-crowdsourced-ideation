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
    IServiceProvider serviceProvider,
    IDatabaseFallbackService fallback,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FallbackAdapter> logger) : IRepositoryAdapter
{
    public ITopicRepository Topics => ResolveActiveAdapter().Topics;
    public IIdeaRepository Ideas => ResolveActiveAdapter().Ideas;
    public IVoteRepository Votes => ResolveActiveAdapter().Votes;
    public IUserRepository Users => ResolveActiveAdapter().Users;

    public Task SaveChangesAsync() => ResolveActiveAdapter().SaveChangesAsync();

    private IRepositoryAdapter ResolveActiveAdapter()
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.ToString() ?? string.Empty;
        var active = fallback.GetActiveDatabase(path);
        logger.LogWarning("Fallback adapter routing request path {Path} to {Database}.", path, active);

        return active switch
        {
            DatabaseType.MySql => serviceProvider.GetRequiredKeyedService<IRepositoryAdapter>("mysql"),
            DatabaseType.MongoDb => serviceProvider.GetRequiredKeyedService<IRepositoryAdapter>("mongo"),
            DatabaseType.BothDown => ThrowBothDown(path),
            _ => throw new InvalidOperationException($"Unsupported database type '{active}' for path '{path}'.")
        };
    }

    private IRepositoryAdapter ThrowBothDown(string path)
    {
        // Middleware should intercept this. This is a safety net to avoid silently using the wrong database.
        logger.LogWarning("Both databases are down; repository access attempted for path {Path}.", path);
        throw new InvalidOperationException("Both databases are down.");
    }
}
