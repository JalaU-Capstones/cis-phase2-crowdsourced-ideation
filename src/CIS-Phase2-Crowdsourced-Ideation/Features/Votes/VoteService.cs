using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

public sealed class VoteUnauthorizedException(string message) : Exception(message);
public sealed class VoteForbiddenException(string message) : Exception(message);
public sealed class VoteNotFoundException(string message) : KeyNotFoundException(message);
public sealed class VoteConflictException(string message) : Exception(message);

public interface IVoteService
{
    Task<IReadOnlyList<VoteResponse>> GetAllAsync();
    Task<IReadOnlyList<VoteResponse>> GetByIdeaIdAsync(Guid ideaId);
    Task<VoteResponse?> GetByIdAsync(Guid voteId);

    Task<VoteResponse> CastVoteAsync(CastVoteRequest request, ClaimsPrincipal user);
    Task<VoteResponse?> UpdateVoteAsync(Guid voteId, UpdateVoteRequest request, ClaimsPrincipal user);
    Task<bool> DeleteVoteAsync(Guid voteId, ClaimsPrincipal user);
}

public sealed class VoteService(IRepositoryAdapter adapter, string version = "v1") : IVoteService
{
    private const string ClosedTopicMessage = "This topic is closed. Voting is no longer allowed.";
    private const string OwnershipMessage = "You can only modify or delete your own vote.";

    private async Task<Guid> ResolveUserIdAsync(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(raw))
            throw new VoteUnauthorizedException("User identity not found or invalid");

        if (Guid.TryParse(raw, out var userId))
            return userId;

        var dbUser = await adapter.Users.GetByLoginAsync(raw);
        if (dbUser is null || !Guid.TryParse(dbUser.Id, out userId))
            throw new VoteUnauthorizedException("User identity not found or invalid");

        return userId;
    }

    public async Task<IReadOnlyList<VoteResponse>> GetAllAsync()
    {
        var votes = await adapter.Votes.GetAllAsync();
        var responses = new List<VoteResponse>();
        foreach (var v in votes)
        {
            var idea = await adapter.Ideas.GetByIdAsync(v.IdeaId);
            var topic = idea != null ? await adapter.Topics.GetByIdAsync(idea.TopicId) : null;
            responses.Add(MapToResponse(v, idea, topic));
        }
        return responses;
    }

    public async Task<IReadOnlyList<VoteResponse>> GetByIdeaIdAsync(Guid ideaId)
    {
        var votes = await adapter.Votes.GetByIdeaIdAsync(ideaId);
        var idea = await adapter.Ideas.GetByIdAsync(ideaId);
        var topic = idea != null ? await adapter.Topics.GetByIdAsync(idea.TopicId) : null;
        
        return votes.Select(v => MapToResponse(v, idea, topic)).ToList();
    }

    public async Task<VoteResponse?> GetByIdAsync(Guid voteId)
    {
        var vote = await adapter.Votes.GetByIdAsync(voteId);
        if (vote == null) return null;

        var idea = await adapter.Ideas.GetByIdAsync(vote.IdeaId);
        var topic = idea != null ? await adapter.Topics.GetByIdAsync(idea.TopicId) : null;

        return MapToResponse(vote, idea, topic);
    }

    public async Task<VoteResponse> CastVoteAsync(CastVoteRequest request, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);

        var idea = await adapter.Ideas.GetByIdAsync(request.IdeaId);
        if (idea is null)
            throw new VoteNotFoundException($"Idea with ID '{request.IdeaId}' not found");

        var topic = await adapter.Topics.GetByIdAsync(idea.TopicId);
        if (topic?.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        var userVotes = await adapter.Votes.GetByIdeaIdAsync(request.IdeaId);
        if (userVotes.Any(v => v.UserId == userId))
            throw new VoteConflictException("User has already voted on this idea.");

        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            IdeaId = request.IdeaId,
            UserId = userId
        };

        await adapter.Votes.AddAsync(vote);
        await adapter.SaveChangesAsync();

        return MapToResponse(vote, idea, topic);
    }

    public async Task<VoteResponse?> UpdateVoteAsync(Guid voteId, UpdateVoteRequest request, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);

        var vote = await adapter.Votes.GetByIdAsync(voteId);
        if (vote is null) return null;

        if (vote.UserId != userId)
            throw new VoteForbiddenException(OwnershipMessage);

        var idea = await adapter.Ideas.GetByIdAsync(vote.IdeaId);
        var topic = idea != null ? await adapter.Topics.GetByIdAsync(idea.TopicId) : null;

        if (topic?.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        if (vote.IdeaId == request.IdeaId)
            return MapToResponse(vote, idea, topic);

        var newIdea = await adapter.Ideas.GetByIdAsync(request.IdeaId);
        if (newIdea is null)
            throw new VoteNotFoundException($"Idea with ID '{request.IdeaId}' not found");

        var newTopic = await adapter.Topics.GetByIdAsync(newIdea.TopicId);
        if (newTopic?.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        var targetVotes = await adapter.Votes.GetByIdeaIdAsync(request.IdeaId);
        if (targetVotes.Any(v => v.Id != voteId && v.UserId == userId))
            throw new VoteConflictException("User has already voted on this idea.");

        vote.IdeaId = request.IdeaId;
        await adapter.Votes.UpdateAsync(vote);
        await adapter.SaveChangesAsync();

        return MapToResponse(vote, newIdea, newTopic);
    }

    public async Task<bool> DeleteVoteAsync(Guid voteId, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);

        var vote = await adapter.Votes.GetByIdAsync(voteId);
        if (vote is null) return false;

        if (vote.UserId != userId)
            throw new VoteForbiddenException(OwnershipMessage);

        var idea = await adapter.Ideas.GetByIdAsync(vote.IdeaId);
        var topic = idea != null ? await adapter.Topics.GetByIdAsync(idea.TopicId) : null;

        if (topic?.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        await adapter.Votes.DeleteAsync(vote);
        await adapter.SaveChangesAsync();
        return true;
    }

    private VoteResponse MapToResponse(Vote v, Idea? idea, Topic? topic) =>
        new(
            v.Id,
            v.IdeaId,
            idea?.Title ?? "N/A",
            idea?.TopicId ?? "N/A",
            topic?.Title ?? "N/A"
        )
        {
            Links = HateoasBuilder.ForVote(v.Id, v.IdeaId, version)
        };
}
