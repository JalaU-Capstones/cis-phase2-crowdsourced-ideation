using System.Reflection;
using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Votes;

public sealed class VoteEndpointsTests
{
    private static MethodInfo InternalMethod(string name) =>
        typeof(VoteEndpoints).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Could not find {name} on VoteEndpoints.");

    private static DefaultHttpContext HttpWithAdapter(IRepositoryAdapter adapter)
    {
        var http = new DefaultHttpContext();
        http.Items["RepositoryAdapter"] = adapter;
        return http;
    }

    private static ClaimsPrincipal UserWithSub(Guid id) =>
        new(new ClaimsIdentity(new[] { new Claim("sub", id.ToString()), new Claim("login", "l1") }, "Test"));

    private static int StatusCodeOf(IResult result) =>
        (result as IStatusCodeHttpResult)?.StatusCode
        ?? throw new InvalidOperationException($"Result type {result.GetType().Name} does not expose a status code.");

    [Fact]
    public async Task HandleCastVote_WhenUnauthenticated_Returns401()
    {
        var adapter = Mock.Of<IRepositoryAdapter>();
        var http = HttpWithAdapter(adapter);
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // no sub/login => unauthorized

        var method = InternalMethod("HandleCastVote");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            new CastVoteRequest(Guid.NewGuid()),
            http,
            user,
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task HandleCastVote_WhenIdeaMissing_Returns404()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        adapter.Setup(a => a.Ideas).Returns(Mock.Of<IIdeaRepository>(r => r.GetByIdAsync(It.IsAny<Guid>()) == Task.FromResult<Idea?>(null)));
        adapter.Setup(a => a.Topics).Returns(Mock.Of<ITopicRepository>());
        adapter.Setup(a => a.Votes).Returns(Mock.Of<IVoteRepository>());
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapter.Setup(a => a.Users).Returns(users.Object);

        var http = HttpWithAdapter(adapter.Object);
        var user = UserWithSub(Guid.NewGuid());

        var method = InternalMethod("HandleCastVote");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            new CastVoteRequest(Guid.NewGuid()),
            http,
            user,
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task HandleCastVote_WhenDuplicateVote_Returns409()
    {
        var userId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();

        var adapter = new Mock<IRepositoryAdapter>();
        adapter.Setup(a => a.Ideas).Returns(Mock.Of<IIdeaRepository>(r => r.GetByIdAsync(ideaId) == Task.FromResult<Idea?>(new Idea { Id = ideaId, TopicId = "t1" })));
        adapter.Setup(a => a.Topics).Returns(Mock.Of<ITopicRepository>(r => r.GetByIdAsync("t1") == Task.FromResult<Topic?>(new Topic { Id = "t1", Status = TopicStatus.OPEN })));
        adapter.Setup(a => a.Votes).Returns(Mock.Of<IVoteRepository>(r => r.GetByIdeaIdAsync(ideaId) == Task.FromResult<IEnumerable<Vote>>(new[] { new Vote { Id = Guid.NewGuid(), IdeaId = ideaId, UserId = userId } })));
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapter.Setup(a => a.Users).Returns(users.Object);

        var http = HttpWithAdapter(adapter.Object);
        var user = UserWithSub(userId);

        var method = InternalMethod("HandleCastVote");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            new CastVoteRequest(ideaId),
            http,
            user,
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status409Conflict);
    }
}
