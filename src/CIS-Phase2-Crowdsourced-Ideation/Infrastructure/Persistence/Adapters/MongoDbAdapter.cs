using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

public class MongoDbAdapter(MongoDbContext context) : IRepositoryAdapter
{
    private readonly Lazy<ITopicRepository> _topics = new(() => new MongoTopicRepository(context));
    private readonly Lazy<IIdeaRepository> _ideas = new(() => new MongoIdeaRepository(context));
    private readonly Lazy<IVoteRepository> _votes = new(() => new MongoVoteRepository(context));
    private readonly Lazy<IUserRepository> _users = new(() => new MongoUserRepository(context));

    public virtual ITopicRepository Topics => _topics.Value;
    public virtual IIdeaRepository Ideas => _ideas.Value;
    public virtual IVoteRepository Votes => _votes.Value;
    public virtual IUserRepository Users => _users.Value;

    public Task SaveChangesAsync() => Task.CompletedTask; // MongoDB is auto-saving
}
