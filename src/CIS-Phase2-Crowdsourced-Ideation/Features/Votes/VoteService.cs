using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

public sealed class VoteService(AppDbContext db) : IVoteService
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

        var dbUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == raw);
        if (dbUser is null || !Guid.TryParse(dbUser.Id, out userId))
            throw new VoteUnauthorizedException("User identity not found or invalid");

        return userId;
    }

    public async Task<IReadOnlyList<VoteResponse>> GetAllAsync()
    {
        var votes = await db.Votes
            .AsNoTracking()
            .Include(v => v.Idea)
            .ThenInclude(i => i.Topic)
            .OrderByDescending(v => v.Id)
            .ToListAsync();

        return votes.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<VoteResponse>> GetByIdeaIdAsync(Guid ideaId)
    {
        var votes = await db.Votes
            .AsNoTracking()
            .Where(v => v.IdeaId == ideaId)
            .Include(v => v.Idea)
            .ThenInclude(i => i.Topic)
            .ToListAsync();

        return votes.Select(MapToResponse).ToList();
    }

    public async Task<VoteResponse?> GetByIdAsync(Guid voteId)
    {
        var vote = await db.Votes
            .AsNoTracking()
            .Where(v => v.Id == voteId)
            .Include(v => v.Idea)
            .ThenInclude(i => i.Topic)
            .FirstOrDefaultAsync();

        return vote is null ? null : MapToResponse(vote);
    }

    public async Task<VoteResponse> CastVoteAsync(CastVoteRequest request, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);

        var idea = await db.Ideas
            .Include(i => i.Topic)
            .FirstOrDefaultAsync(i => i.Id == request.IdeaId);

        if (idea is null)
            throw new VoteNotFoundException($"Idea with ID '{request.IdeaId}' not found");

        if (idea.Topic.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        var exists = await db.Votes.AnyAsync(v => v.IdeaId == request.IdeaId && v.UserId == userId);
        if (exists)
            throw new VoteConflictException("User has already voted on this idea.");

        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            IdeaId = request.IdeaId,
            UserId = userId
        };

        db.Votes.Add(vote);
        await db.SaveChangesAsync();

        // Re-load navigation for response mapping (InMemory provider may not auto-fixup in some test setups).
        vote.Idea = idea;
        return MapToResponse(vote);
    }

    public async Task<VoteResponse?> UpdateVoteAsync(Guid voteId, UpdateVoteRequest request, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);

        var vote = await db.Votes
            .Include(v => v.Idea)
            .ThenInclude(i => i.Topic)
            .FirstOrDefaultAsync(v => v.Id == voteId);

        if (vote is null)
            return null;

        if (vote.UserId != userId)
            throw new VoteForbiddenException(OwnershipMessage);

        if (vote.Idea.Topic.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        if (vote.IdeaId == request.IdeaId)
            return MapToResponse(vote);

        var newIdea = await db.Ideas
            .Include(i => i.Topic)
            .FirstOrDefaultAsync(i => i.Id == request.IdeaId);

        if (newIdea is null)
            throw new VoteNotFoundException($"Idea with ID '{request.IdeaId}' not found");

        if (newIdea.Topic.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        var exists = await db.Votes.AnyAsync(v => v.Id != voteId && v.IdeaId == request.IdeaId && v.UserId == userId);
        if (exists)
            throw new VoteConflictException("User has already voted on this idea.");

        vote.IdeaId = request.IdeaId;
        vote.Idea = newIdea;
        await db.SaveChangesAsync();

        return MapToResponse(vote);
    }

    public async Task<bool> DeleteVoteAsync(Guid voteId, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);

        var vote = await db.Votes
            .Include(v => v.Idea)
            .ThenInclude(i => i.Topic)
            .FirstOrDefaultAsync(v => v.Id == voteId);

        if (vote is null)
            return false;

        if (vote.UserId != userId)
            throw new VoteForbiddenException(OwnershipMessage);

        if (vote.Idea.Topic.Status == TopicStatus.CLOSED)
            throw new VoteForbiddenException(ClosedTopicMessage);

        db.Votes.Remove(vote);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Maps a Vote entity to a VoteResponse DTO including HATEOAS links (US 3.2 - AC-8 / AC-9).
    /// </summary>
    private static VoteResponse MapToResponse(Vote v) =>
        new(
            v.Id,
            v.IdeaId,
            v.Idea.Title,
            v.Idea.TopicId,
            v.Idea.Topic.Title
        )
        {
            Links = HateoasBuilder.ForVote(v.Id, v.IdeaId)
        };
}