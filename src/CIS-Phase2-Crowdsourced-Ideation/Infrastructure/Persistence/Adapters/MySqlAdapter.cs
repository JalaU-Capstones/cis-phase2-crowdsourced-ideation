using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

public class MySqlAdapter(AppDbContext context) : IRepositoryAdapter
{
    private readonly Lazy<ITopicRepository> _topics = new(() => new TopicRepository(context));
    private readonly Lazy<IIdeaRepository> _ideas = new(() => new IdeaRepository(context));
    private readonly Lazy<IVoteRepository> _votes = new(() => new VoteRepository(context));
    private readonly Lazy<IUserRepository> _users = new(() => new UserRepository(context));

    public ITopicRepository Topics => _topics.Value;
    public IIdeaRepository Ideas => _ideas.Value;
    public IVoteRepository Votes => _votes.Value;
    public IUserRepository Users => _users.Value;

    public Task SaveChangesAsync() => context.SaveChangesAsync();
}
