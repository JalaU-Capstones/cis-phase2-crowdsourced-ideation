using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public interface IIdeaService
{
    Task<IdeaResponse> CreateIdeaAsync(CreateIdeaRequest request, ClaimsPrincipal user);
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
        
        var topicExists = await context.Topics.AnyAsync(t => t.Id == request.TopicId);
        if (!topicExists)
        {
             throw new ArgumentException("Topic not found");
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

    public async Task<IEnumerable<IdeaResponse>> GetIdeasByTopicIdAsync(string topicId)
    {
        return await context.Set<Idea>()
            .Where(i => i.TopicId == topicId)
            .Select(i => MapToResponse(i))
            .ToListAsync();
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

        var userId = await ResolveUserIdAsync(user);
        if (idea.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to modify this idea");
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
