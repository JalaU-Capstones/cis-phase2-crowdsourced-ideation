using System.Reflection;
using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Ideas;

public sealed class IdeaEndpointsTests
{
    private static MethodInfo PrivateMethod(string name) =>
        typeof(IdeaEndpoints).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Could not find {name} on IdeaEndpoints.");

    private static ClaimsPrincipal UserWithSub(Guid id) =>
        new(new ClaimsIdentity(new[] { new Claim("sub", id.ToString()), new Claim("login", "l1"), new Claim("name", "n1") }, "Test"));

    private static DefaultHttpContext HttpWithAdapter(IRepositoryAdapter adapter)
    {
        var http = new DefaultHttpContext();
        http.Items["RepositoryAdapter"] = adapter;
        return http;
    }

    private static int StatusCodeOf(IResult result) =>
        (result as IStatusCodeHttpResult)?.StatusCode
        ?? throw new InvalidOperationException($"Result type {result.GetType().Name} does not expose a status code.");

    [Fact]
    public async Task CreateIdea_WhenMissingFields_Returns400()
    {
        var http = HttpWithAdapter(Mock.Of<IRepositoryAdapter>());
        var user = UserWithSub(Guid.NewGuid());

        var method = PrivateMethod("CreateIdea");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            new CreateIdeaRequest("", "t", "d"),
            http,
            user,
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateIdea_WhenTopicMissing_Returns400_WithArgumentMessage()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        var topics = new Mock<ITopicRepository>();
        topics.Setup(t => t.GetByIdAsync("t1")).ReturnsAsync((Topic?)null);

        adapter.Setup(a => a.Topics).Returns(topics.Object);
        adapter.Setup(a => a.Ideas).Returns(Mock.Of<IIdeaRepository>());
        adapter.Setup(a => a.Votes).Returns(Mock.Of<IVoteRepository>());
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapter.Setup(a => a.Users).Returns(users.Object);
        adapter.Setup(a => a.SaveChangesAsync()).Returns(Task.CompletedTask);

        var http = HttpWithAdapter(adapter.Object);
        var user = UserWithSub(Guid.NewGuid());

        var method = PrivateMethod("CreateIdea");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            new CreateIdeaRequest("t1", "Title", "Desc"),
            http,
            user,
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateIdea_WhenTopicClosed_Returns403()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        var topics = new Mock<ITopicRepository>();
        topics.Setup(t => t.GetByIdAsync("t1"))
            .ReturnsAsync(new Topic { Id = "t1", Status = TopicStatus.CLOSED });

        adapter.Setup(a => a.Topics).Returns(topics.Object);
        adapter.Setup(a => a.Ideas).Returns(Mock.Of<IIdeaRepository>());
        adapter.Setup(a => a.Votes).Returns(Mock.Of<IVoteRepository>());
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        adapter.Setup(a => a.Users).Returns(users.Object);

        var http = HttpWithAdapter(adapter.Object);
        var user = UserWithSub(Guid.NewGuid());

        var method = PrivateMethod("CreateIdea");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            new CreateIdeaRequest("t1", "Title", "Desc"),
            http,
            user,
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetAllIdeas_WhenInvalidPage_Returns400()
    {
        var http = HttpWithAdapter(Mock.Of<IRepositoryAdapter>());

        var method = PrivateMethod("GetAllIdeas");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            http,
            -1,
            10,
            "updatedAt",
            "desc",
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetIdea_WhenNotFound_Returns404()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        adapter.Setup(a => a.Ideas).Returns(Mock.Of<IIdeaRepository>(r => r.GetByIdAsync(It.IsAny<Guid>()) == Task.FromResult<Idea?>(null)));
        adapter.Setup(a => a.Topics).Returns(Mock.Of<ITopicRepository>());
        adapter.Setup(a => a.Votes).Returns(Mock.Of<IVoteRepository>());
        adapter.Setup(a => a.Users).Returns(Mock.Of<IUserRepository>());

        var http = HttpWithAdapter(adapter.Object);
        var id = Guid.NewGuid();

        var method = PrivateMethod("GetIdea");
        var result = await (Task<IResult>)method.Invoke(null, new object?[] { id, http, "v1" })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteIdea_WhenIdeaMissing_Returns404()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        adapter.Setup(a => a.Ideas).Returns(Mock.Of<IIdeaRepository>(r => r.GetByIdAsync(It.IsAny<Guid>()) == Task.FromResult<Idea?>(null)));
        adapter.Setup(a => a.Topics).Returns(Mock.Of<ITopicRepository>());
        adapter.Setup(a => a.Votes).Returns(Mock.Of<IVoteRepository>());
        adapter.Setup(a => a.Users).Returns(Mock.Of<IUserRepository>());

        var http = HttpWithAdapter(adapter.Object);
        var user = UserWithSub(Guid.NewGuid());

        var method = PrivateMethod("DeleteIdea");
        var result = await (Task<IResult>)method.Invoke(null, new object?[] { Guid.NewGuid(), http, user, "v1" })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CreateIdea_WhenValid_Returns201()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        var topics = new Mock<ITopicRepository>();
        topics.Setup(t => t.GetByIdAsync("t1")).ReturnsAsync(new Topic { Id = "t1", Status = TopicStatus.OPEN });

        Idea? savedIdea = null;
        var ideas = new Mock<IIdeaRepository>();
        ideas.Setup(r => r.AddAsync(It.IsAny<Idea>()))
            .Callback<Idea>(i => savedIdea = i)
            .Returns(Task.CompletedTask);

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        adapter.Setup(a => a.Topics).Returns(topics.Object);
        adapter.Setup(a => a.Ideas).Returns(ideas.Object);
        adapter.Setup(a => a.Votes).Returns(Mock.Of<IVoteRepository>());
        adapter.Setup(a => a.Users).Returns(users.Object);
        adapter.Setup(a => a.SaveChangesAsync()).Returns(Task.CompletedTask);

        var http = HttpWithAdapter(adapter.Object);
        var user = UserWithSub(Guid.NewGuid());

        var method = PrivateMethod("CreateIdea");
        var result = await (Task<IResult>)method.Invoke(null, new object?[]
        {
            new CreateIdeaRequest("t1", "Title", "Desc"),
            http,
            user,
            "v1"
        })!;

        StatusCodeOf(result).Should().Be(StatusCodes.Status201Created);
        savedIdea.Should().NotBeNull();
    }
}
