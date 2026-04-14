using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Features;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public interface IIdeaService
{
    Task<IdeaResponse> CreateIdeaAsync(CreateIdeaRequest request, ClaimsPrincipal user);
    Task<PagedResponse<IdeaResponse>> GetAllIdeasAsync(int? page, int? size, string? sortBy, string? order);
    Task<IdeaResponse?> GetIdeaByIdAsync(Guid id);
    Task<IEnumerable<IdeaResponse>> GetIdeasByTopicIdAsync(string topicId);
    Task<IdeaResponse?> UpdateIdeaAsync(Guid id, UpdateIdeaRequest request, ClaimsPrincipal user);
    Task<bool> DeleteIdeaAsync(Guid id, ClaimsPrincipal user);
}

public class IdeaService(AppDbContext context) : IIdeaService
{
    private static readonly string[] ValidSortFields = ["updatedAt"];
    private static readonly string[] ValidOrders = ["asc", "desc"];
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

        // Topic is OPEN at this point (CLOSED would have thrown above).
        return MapToResponse(idea, topicIsOpen: true);
    }

    public async Task<IdeaResponse?> GetIdeaByIdAsync(Guid id)
    {
        var idea = await context.Set<Idea>().FindAsync(id);
        if (idea is null) return null;

        // Fetch topic status to determine whether to include the 'vote' link 
        var topicStatus = await context.Topics
            .AsNoTracking()
            .Where(t => t.Id == idea.TopicId)
            .Select(t => (TopicStatus?)t.Status)
            .FirstOrDefaultAsync();

        return MapToResponse(idea, topicIsOpen: topicStatus == TopicStatus.OPEN);
    }

    public async Task<PagedResponse<IdeaResponse>> GetAllIdeasAsync(int? page, int? size, string? sortBy, string? order)
    {
        // 1. Validate pagination
        var currentPage = page ?? 0;
        var pageSize    = size ?? 10;

        if (currentPage < 0)
            throw new ArgumentException("page must be >= 0.");
        if (pageSize <= 0)
            throw new ArgumentException("size must be >= 1.");

        // 2. Validate sorting
        var sortField = sortBy ?? "updatedAt";
        var sortOrder = order  ?? "desc";

        if (!ValidSortFields.Contains(sortField))
        throw new ArgumentException($"sortBy must be one of: {string.Join(", ", ValidSortFields)}.");
        if (!ValidOrders.Contains(sortOrder))
        throw new ArgumentException($"order must be 'asc' or 'desc'.");

        // 3. Query base
        var query = context.Set<Idea>().AsNoTracking().AsQueryable();

        // 4. Apply sorting
        query = (sortField, sortOrder) switch
        {
            ("updatedAt", "asc")  => query.OrderBy(i => i.UpdatedAt),
            _                     => query.OrderByDescending(i => i.UpdatedAt),
        };

        // 5. Count total BEFORE pagination
        var totalItems = await query.CountAsync();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        // 6. Aplly pagination
        var ideas = await query
            .Skip(currentPage * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 7. Batch-fetch topic statuses to build HATEOAS links (US 3.2) without N+1 queries.
        var topicIds = ideas.Select(i => i.TopicId).Distinct().ToList();
        var topicStatuses = topicIds.Count > 0
            ? await context.Topics
                .AsNoTracking()
                .Where(t => topicIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Status)
            : new Dictionary<string, TopicStatus>();

        return new PagedResponse<IdeaResponse>(
            Data:        ideas.Select(i =>
                MapToResponse(i, topicIsOpen: topicStatuses.TryGetValue(i.TopicId, out var s) && s == TopicStatus.OPEN)),
            CurrentPage: currentPage,
            PageSize:    pageSize,
            TotalItems:  totalItems,
            TotalPages:  totalPages
        );
    }

    public async Task<IEnumerable<IdeaResponse>> GetIdeasByTopicIdAsync(string topicId)
    {
        var ideas = await context.Set<Idea>()
            .AsNoTracking()
            .Where(i => i.TopicId == topicId)
            .ToListAsync();

        // Fetch topic status once for the whole set (US 3.2).
        var topicStatus = await context.Topics
            .AsNoTracking()
            .Where(t => t.Id == topicId)
            .Select(t => (TopicStatus?)t.Status)
            .FirstOrDefaultAsync();

        var isOpen = topicStatus == TopicStatus.OPEN;
        return ideas.Select(i => MapToResponse(i, topicIsOpen: isOpen));
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

        // Topic is OPEN at this point (CLOSED would have thrown above).
        return MapToResponse(idea, topicIsOpen: true);
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

    /// <summary>
    /// Maps an Idea entity to an IdeaResponse DTO including HATEOAS links (US 3.2).
    /// <paramref name="topicIsOpen"/> controls whether the 'vote' link is included.
    /// </summary>
    private static IdeaResponse MapToResponse(Idea idea, bool topicIsOpen) =>
        new IdeaResponse(
            idea.Id,
            idea.TopicId,
            idea.OwnerId,
            idea.Title,
            idea.Description,
            idea.CreatedAt,
            idea.UpdatedAt,
            idea.IsWinning
        )
        {
            Links = HateoasBuilder.ForIdea(idea.Id, idea.TopicId, topicIsOpen)
        };
}