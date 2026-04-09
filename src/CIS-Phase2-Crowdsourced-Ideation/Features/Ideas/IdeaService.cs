using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public interface IIdeaService
{
    Task<IdeaResponse> CreateIdeaAsync(CreateIdeaRequest request, ClaimsPrincipal user);
    Task<IEnumerable<IdeaResponse>> GetAllIdeasAsync();
    Task<IdeaResponse?> GetIdeaByIdAsync(Guid id);
    Task<IEnumerable<IdeaResponse>> GetIdeasByTopicIdAsync(string topicId);
    Task<IdeaResponse?> UpdateIdeaAsync(Guid id, UpdateIdeaRequest request, ClaimsPrincipal user);
    Task<bool> DeleteIdeaAsync(Guid id, ClaimsPrincipal user);
}

public class IdeaService(AppDbContext context) : IIdeaService
{
    private async Task<Guid> ResolveUserIdAsync(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(raw))
            throw new UnauthorizedAccessException("User identity not found or invalid");

        // Tests and some JWT setups provide the user id directly as a GUID.
        if (Guid.TryParse(raw, out var userId))
            return userId;

        // Phase 1 integration typically provides login/subject; map to DB user record to get the GUID id.
        var dbUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == raw);
        if (dbUser is null || !Guid.TryParse(dbUser.Id, out userId))
            throw new UnauthorizedAccessException("User identity not found or invalid");

        return userId;
    }

    public async Task<IdeaResponse> CreateIdeaAsync(CreateIdeaRequest request, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);
        
        var topic = await context.Topics
            .AsNoTracking()
            .Where(t => t.Id == request.TopicId)
            .Select(t => new { t.Id, t.Status })
            .FirstOrDefaultAsync();

        if (topic is null)
        {
             throw new ArgumentException("Topic not found");
        }

        if (topic.Status == TopicStatus.CLOSED)
        {
            throw new UnauthorizedAccessException("This topic is closed. No modifications allowed.");
        }

        var idea = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = request.TopicId,
            OwnerId = userId,
            Title = request.Title,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsWinning = false
        };

        context.Set<Idea>().Add(idea);
        await context.SaveChangesAsync();

        return MapToResponse(idea);
    }

    public async Task<IdeaResponse?> GetIdeaByIdAsync(Guid id)
    {
        var idea = await context.Set<Idea>().FindAsync(id);
        return idea == null ? null : MapToResponse(idea);
    }

    public async Task<IEnumerable<IdeaResponse>> GetAllIdeasAsync()
    {
        var ideas = await context.Set<Idea>()
            .AsNoTracking()
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync();
        return ideas.Select(MapToResponse);
    }

    public async Task<IEnumerable<IdeaResponse>> GetIdeasByTopicIdAsync(string topicId)
    {
        var ideas = await context.Set<Idea>()
            .AsNoTracking()
            .Where(i => i.TopicId == topicId)
            .ToListAsync();
        return ideas.Select(MapToResponse);
    }

    public async Task<IdeaResponse?> UpdateIdeaAsync(Guid id, UpdateIdeaRequest request, ClaimsPrincipal user)
    {
        // Avoid side-effects when the same DbContext instance already tracks the entity (unit tests reuse the context).
        var tracked = context.ChangeTracker.Entries<Idea>().FirstOrDefault(e => e.Entity.Id == id);
        if (tracked is not null)
        {
            tracked.State = EntityState.Detached;
        }

        var idea = await context.Set<Idea>().FindAsync(id);
        if (idea == null) return null;

        // Topic closed protection: updates are forbidden when the topic is CLOSED.
        var topicStatus = await context.Topics
            .AsNoTracking()
            .Where(t => t.Id == idea.TopicId)
            .Select(t => t.Status)
            .FirstOrDefaultAsync();
        if (topicStatus == TopicStatus.CLOSED)
        {
            throw new UnauthorizedAccessException("This topic is closed. No modifications allowed.");
        }

        var userId = await ResolveUserIdAsync(user);
        if (idea.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to modify this idea");
        }

        idea.Title = request.Title;
        idea.Description = request.Description;
        var now = DateTime.UtcNow;
        idea.UpdatedAt = now <= idea.UpdatedAt ? idea.UpdatedAt.AddTicks(1) : now;

        await context.SaveChangesAsync();
        return MapToResponse(idea);
    }

    public async Task<bool> DeleteIdeaAsync(Guid id, ClaimsPrincipal user)
    {
        var idea = await context.Set<Idea>().FindAsync(id);
        if (idea == null) return false;

        // Topic closed protection: deletes are forbidden when the topic is CLOSED.
        var topicStatus = await context.Topics
            .AsNoTracking()
            .Where(t => t.Id == idea.TopicId)
            .Select(t => t.Status)
            .FirstOrDefaultAsync();
        if (topicStatus == TopicStatus.CLOSED)
        {
            throw new UnauthorizedAccessException("This topic is closed. No modifications allowed.");
        }

        var userId = await ResolveUserIdAsync(user);
        if (idea.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to modify this idea");
        }

        // Legacy schema note:
        // The MySQL foreign key in init.sql does NOT specify ON DELETE CASCADE for ideas -> votes.
        // To keep the intended behavior (deleting an idea removes its votes), delete votes first.
        if (!context.Database.IsRelational())
        {
            var votes = await context.Votes.Where(v => v.IdeaId == id).ToListAsync();
            context.Votes.RemoveRange(votes);
        }
        else
        {
            await context.Votes.Where(v => v.IdeaId == id).ExecuteDeleteAsync();
        }

        context.Set<Idea>().Remove(idea);
        await context.SaveChangesAsync();
        return true;
    }

    private static IdeaResponse MapToResponse(Idea idea) =>
        new IdeaResponse(
            idea.Id,
            idea.TopicId,
            idea.OwnerId,
            idea.Title,
            idea.Description,
            idea.CreatedAt,
            idea.UpdatedAt,
            idea.IsWinning
        );
}
