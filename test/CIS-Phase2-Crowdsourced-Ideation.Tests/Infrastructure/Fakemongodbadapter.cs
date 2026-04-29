using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure;

/// <summary>
/// Implementación fake de IRepositoryAdapter para tests de integración v2.
/// Recibe los repositorios mockeados directamente — no necesita MongoDbContext.
/// Esto evita el error "Cannot instantiate proxy of class MongoDbContext"
/// ya que MongoDbContext no tiene constructor sin parámetros.
/// </summary>
internal sealed class FakeMongoDbAdapter(
    ITopicRepository topics,
    IIdeaRepository  ideas,
    IVoteRepository  votes,
    IUserRepository  users) : IRepositoryAdapter
{
    public ITopicRepository Topics => topics;
    public IIdeaRepository  Ideas  => ideas;
    public IVoteRepository  Votes  => votes;
    public IUserRepository  Users  => users;

    public Task SaveChangesAsync() => Task.CompletedTask;
}