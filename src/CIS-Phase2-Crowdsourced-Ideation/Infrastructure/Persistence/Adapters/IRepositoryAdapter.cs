using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

public interface IRepositoryAdapter
{
    ITopicRepository Topics { get; }
    IIdeaRepository Ideas { get; }
    IVoteRepository Votes { get; }
    IUserRepository Users { get; }
    Task SaveChangesAsync();
}
