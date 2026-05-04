using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using FluentAssertions;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Votes;

public sealed class VoteServiceTests
{
    private readonly Mock<IRepositoryAdapter> _adapterMock = new();
    private readonly Mock<IVoteRepository> _voteRepoMock = new();
    private readonly Mock<IIdeaRepository> _ideaRepoMock = new();
    private readonly Mock<ITopicRepository> _topicRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly VoteService _service;

    public VoteServiceTests()
    {
        _adapterMock.Setup(a => a.Votes).Returns(_voteRepoMock.Object);
        _adapterMock.Setup(a => a.Ideas).Returns(_ideaRepoMock.Object);
        _adapterMock.Setup(a => a.Topics).Returns(_topicRepoMock.Object);
        _adapterMock.Setup(a => a.Users).Returns(_userRepoMock.Object);
        _service = new VoteService(_adapterMock.Object);
    }

    private static ClaimsPrincipal CreateUser(string id, string login = "test")
    {
        var claims = new List<Claim>
        {
            new("sub", id),
            new("login", login)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedResponses()
    {
        var voteId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();
        var votes = new List<Vote> { new() { Id = voteId, IdeaId = ideaId, UserId = Guid.NewGuid() } };
        _voteRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(votes);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, Title = "Idea 1", TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Title = "Topic 1" });

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(voteId);
        result[0].IdeaTitle.Should().Be("Idea 1");
        result[0].TopicTitle.Should().Be("Topic 1");
    }

    [Fact]
    public async Task GetByIdeaIdAsync_ReturnsMappedResponses()
    {
        var ideaId = Guid.NewGuid();
        var votes = new List<Vote> { new() { Id = Guid.NewGuid(), IdeaId = ideaId, UserId = Guid.NewGuid() } };
        _voteRepoMock.Setup(r => r.GetByIdeaIdAsync(ideaId)).ReturnsAsync(votes);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, Title = "Idea 1", TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Title = "Topic 1" });

        var result = await _service.GetByIdeaIdAsync(ideaId);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedResponse()
    {
        var voteId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();
        var vote = new Vote { Id = voteId, IdeaId = ideaId, UserId = Guid.NewGuid() };
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(vote);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, Title = "Idea 1", TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Title = "Topic 1" });

        var result = await _service.GetByIdAsync(voteId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(voteId);
    }

    [Fact]
    public async Task CastVoteAsync_WhenIdeaNotFound_ThrowsVoteNotFoundException()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Idea?)null);

        var act = async () => await _service.CastVoteAsync(new CastVoteRequest(Guid.NewGuid()), user);

        await act.Should().ThrowAsync<VoteNotFoundException>();
    }

    [Fact]
    public async Task CastVoteAsync_WhenTopicClosed_ThrowsVoteForbiddenException()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var ideaId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.CLOSED });

        var act = async () => await _service.CastVoteAsync(new CastVoteRequest(ideaId), user);

        await act.Should().ThrowAsync<VoteForbiddenException>().WithMessage("This topic is closed*");
    }

    [Fact]
    public async Task CastVoteAsync_WhenDuplicateVote_ThrowsVoteConflictException()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var ideaId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.OPEN });
        _voteRepoMock.Setup(r => r.GetByIdeaIdAsync(ideaId)).ReturnsAsync(new List<Vote> { new() { UserId = userId } });

        var act = async () => await _service.CastVoteAsync(new CastVoteRequest(ideaId), user);

        await act.Should().ThrowAsync<VoteConflictException>();
    }

    [Fact]
    public async Task CastVoteAsync_WhenValid_CreatesVoteAndSaves()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var ideaId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId))
            .ReturnsAsync(new Idea { Id = ideaId, TopicId = "T1", Title = "Idea" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1"))
            .ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.OPEN, Title = "Topic" });
        _voteRepoMock.Setup(r => r.GetByIdeaIdAsync(ideaId)).ReturnsAsync(new List<Vote>());

        var result = await _service.CastVoteAsync(new CastVoteRequest(ideaId), user);

        result.IdeaId.Should().Be(ideaId);
        _voteRepoMock.Verify(r => r.AddAsync(It.Is<Vote>(v => v.IdeaId == ideaId && v.UserId == userId)), Times.Once);
        _adapterMock.Verify(a => a.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateVoteAsync_WhenNotOwner_ThrowsVoteForbiddenException()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(new Vote { Id = voteId, UserId = otherUserId });

        var act = async () => await _service.UpdateVoteAsync(voteId, new UpdateVoteRequest(Guid.NewGuid()), user);

        await act.Should().ThrowAsync<VoteForbiddenException>().WithMessage("You can only modify or delete your own vote.");
    }

    [Fact]
    public async Task UpdateVoteAsync_WhenVoteMissing_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Vote?)null);

        (await _service.UpdateVoteAsync(Guid.NewGuid(), new UpdateVoteRequest(Guid.NewGuid()), user)).Should().BeNull();
    }

    [Fact]
    public async Task UpdateVoteAsync_WhenTopicClosed_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(new Vote { Id = voteId, UserId = userId, IdeaId = ideaId });
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.CLOSED });

        var act = async () => await _service.UpdateVoteAsync(voteId, new UpdateVoteRequest(Guid.NewGuid()), user);
        await act.Should().ThrowAsync<VoteForbiddenException>().WithMessage("This topic is closed*");
    }

    [Fact]
    public async Task UpdateVoteAsync_WhenSameIdeaId_ReturnsResponseWithoutSaving()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(new Vote { Id = voteId, UserId = userId, IdeaId = ideaId });
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, TopicId = "T1", Title = "Idea" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.OPEN, Title = "Topic" });

        var result = await _service.UpdateVoteAsync(voteId, new UpdateVoteRequest(ideaId), user);

        result.Should().NotBeNull();
        _voteRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Vote>()), Times.Never);
        _adapterMock.Verify(a => a.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateVoteAsync_WhenNewIdeaMissing_ThrowsNotFound()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();
        var currentIdeaId = Guid.NewGuid();
        var newIdeaId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(new Vote { Id = voteId, UserId = userId, IdeaId = currentIdeaId });
        _ideaRepoMock.Setup(r => r.GetByIdAsync(currentIdeaId)).ReturnsAsync(new Idea { Id = currentIdeaId, TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.OPEN });
        _ideaRepoMock.Setup(r => r.GetByIdAsync(newIdeaId)).ReturnsAsync((Idea?)null);

        var act = async () => await _service.UpdateVoteAsync(voteId, new UpdateVoteRequest(newIdeaId), user);
        await act.Should().ThrowAsync<VoteNotFoundException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateVoteAsync_WhenTargetAlreadyVoted_ThrowsConflict()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();
        var currentIdeaId = Guid.NewGuid();
        var newIdeaId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(new Vote { Id = voteId, UserId = userId, IdeaId = currentIdeaId });
        _ideaRepoMock.Setup(r => r.GetByIdAsync(currentIdeaId)).ReturnsAsync(new Idea { Id = currentIdeaId, TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.OPEN });

        _ideaRepoMock.Setup(r => r.GetByIdAsync(newIdeaId)).ReturnsAsync(new Idea { Id = newIdeaId, TopicId = "T2" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T2")).ReturnsAsync(new Topic { Id = "T2", Status = TopicStatus.OPEN });
        _voteRepoMock.Setup(r => r.GetByIdeaIdAsync(newIdeaId)).ReturnsAsync(new List<Vote>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, IdeaId = newIdeaId }
        });

        var act = async () => await _service.UpdateVoteAsync(voteId, new UpdateVoteRequest(newIdeaId), user);
        await act.Should().ThrowAsync<VoteConflictException>();
    }

    [Fact]
    public async Task DeleteVoteAsync_WhenFoundAndOwner_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();
        var vote = new Vote { Id = voteId, UserId = userId, IdeaId = Guid.NewGuid() };
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(vote);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(vote.IdeaId)).ReturnsAsync(new Idea { Id = vote.IdeaId, TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.OPEN });

        var result = await _service.DeleteVoteAsync(voteId, user);

        result.Should().BeTrue();
        _voteRepoMock.Verify(r => r.DeleteAsync(vote), Times.Once);
        _adapterMock.Verify(a => a.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteVoteAsync_WhenVoteMissing_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Vote?)null);

        (await _service.DeleteVoteAsync(Guid.NewGuid(), user)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteVoteAsync_WhenNotOwner_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(new Vote { Id = voteId, UserId = Guid.NewGuid() });

        var act = async () => await _service.DeleteVoteAsync(voteId, user);
        await act.Should().ThrowAsync<VoteForbiddenException>().WithMessage("You can only modify or delete your own vote.");
    }

    [Fact]
    public async Task DeleteVoteAsync_WhenTopicClosed_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        var user = CreateUser(userId.ToString());
        var voteId = Guid.NewGuid();
        var ideaId = Guid.NewGuid();
        var vote = new Vote { Id = voteId, UserId = userId, IdeaId = ideaId };

        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);
        _voteRepoMock.Setup(r => r.GetByIdAsync(voteId)).ReturnsAsync(vote);
        _ideaRepoMock.Setup(r => r.GetByIdAsync(ideaId)).ReturnsAsync(new Idea { Id = ideaId, TopicId = "T1" });
        _topicRepoMock.Setup(r => r.GetByIdAsync("T1")).ReturnsAsync(new Topic { Id = "T1", Status = TopicStatus.CLOSED });

        var act = async () => await _service.DeleteVoteAsync(voteId, user);
        await act.Should().ThrowAsync<VoteForbiddenException>().WithMessage("This topic is closed*");
    }
}
