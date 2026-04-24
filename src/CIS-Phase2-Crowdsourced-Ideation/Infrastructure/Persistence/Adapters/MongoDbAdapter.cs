using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

public class MongoDbAdapter(MongoDbContext context) : IRepositoryAdapter
{
    private readonly Lazy<ITopicRepository> _topics = new(() => new MongoTopicRepository(context));
    private readonly Lazy<IIdeaRepository> _ideas = new(() => new MongoIdeaRepository(context));
    private readonly Lazy<IVoteRepository> _votes = new(() => new MongoVoteRepository(context));
    private readonly Lazy<IUserRepository> _users = new(() => new MongoUserRepository(context));

    public ITopicRepository Topics => _topics.Value;
    public IIdeaRepository Ideas => _ideas.Value;
    public IVoteRepository Votes => _votes.Value;
    public IUserRepository Users => _users.Value;

    public Task SaveChangesAsync() => Task.CompletedTask; // MongoDB is auto-saving
}
