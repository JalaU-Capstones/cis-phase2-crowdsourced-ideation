using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Features;
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

public class IdeaService(IRepositoryAdapter adapter, string version = "v1") : IIdeaService
{
    private static readonly string[] ValidSortFields = ["updatedAt"];
    private static readonly string[] ValidOrders = ["asc", "desc"];
    private async Task<Guid> ResolveUserIdAsync(ClaimsPrincipal user)
    {
        return await UserIdentityResolver.ResolveOrProvisionUserIdAsync(adapter, user);
    }

    public async Task<IdeaResponse> CreateIdeaAsync(CreateIdeaRequest request, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);
        
        var topic = await adapter.Topics.GetByIdAsync(request.TopicId);
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

        await adapter.Ideas.AddAsync(idea);
        await adapter.SaveChangesAsync();

        // Topic is OPEN at this point (CLOSED would have thrown above).
        return MapToResponse(idea, topicIsOpen: true);
    }

    public async Task<IdeaResponse?> GetIdeaByIdAsync(Guid id)
    {
        var idea = await adapter.Ideas.GetByIdAsync(id);
        if (idea is null) return null;

        // Fetch topic status to determine whether to include the 'vote' link 
        var topic = await adapter.Topics.GetByIdAsync(idea.TopicId);

        return MapToResponse(idea, topicIsOpen: topic?.Status == TopicStatus.OPEN);
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

        // For simplicity, get all and sort in memory. In production, implement sorting in repo.
        var allIdeas = await adapter.Ideas.GetAllAsync();
        var query = allIdeas.AsQueryable();

        // 4. Apply sorting
        query = (sortField, sortOrder) switch
        {
            ("updatedAt", "asc")  => query.OrderBy(i => i.UpdatedAt),
            _                     => query.OrderByDescending(i => i.UpdatedAt),
        };

        // 5. Count total BEFORE pagination
        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        // 6. Apply pagination
        var ideas = query
            .Skip(currentPage * pageSize)
            .Take(pageSize)
            .ToList();

        // 7. Batch-fetch topic statuses to build HATEOAS links (US 3.2) without N+1 queries.
        var topicIds = ideas.Select(i => i.TopicId).Distinct().ToList();
        var topicStatuses = new Dictionary<string, TopicStatus>();
        foreach (var tid in topicIds)
        {
            var t = await adapter.Topics.GetByIdAsync(tid);
            if (t != null) topicStatuses[tid] = t.Status;
        }

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
        var ideas = await adapter.Ideas.GetByTopicIdAsync(topicId);

        // Fetch topic status once for the whole set (US 3.2).
        var topic = await adapter.Topics.GetByIdAsync(topicId);

        var isOpen = topic?.Status == TopicStatus.OPEN;
        return ideas.Select(i => MapToResponse(i, topicIsOpen: isOpen));
    }

    public async Task<IdeaResponse?> UpdateIdeaAsync(Guid id, UpdateIdeaRequest request, ClaimsPrincipal user)
    {
        var idea = await adapter.Ideas.GetByIdAsync(id);
        if (idea == null) return null;

        // Topic closed protection: updates are forbidden when the topic is CLOSED.
        var topic = await adapter.Topics.GetByIdAsync(idea.TopicId);
        if (topic?.Status == TopicStatus.CLOSED)
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

        await adapter.Ideas.UpdateAsync(idea);
        await adapter.SaveChangesAsync();

        // Topic is OPEN at this point (CLOSED would have thrown above).
        return MapToResponse(idea, topicIsOpen: true);
    }

    public async Task<bool> DeleteIdeaAsync(Guid id, ClaimsPrincipal user)
    {
        var idea = await adapter.Ideas.GetByIdAsync(id);
        if (idea == null) return false;

        // Topic closed protection: deletes are forbidden when the topic is CLOSED.
        var topic = await adapter.Topics.GetByIdAsync(idea.TopicId);
        if (topic?.Status == TopicStatus.CLOSED)
        {
            throw new UnauthorizedAccessException("This topic is closed. No modifications allowed.");
        }

        var userId = await ResolveUserIdAsync(user);
        if (idea.OwnerId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to modify this idea");
        }

        // Delete votes first
        var votes = await adapter.Votes.GetByIdeaIdAsync(id);
        foreach (var vote in votes)
        {
            await adapter.Votes.DeleteAsync(vote);
        }

        await adapter.Ideas.DeleteAsync(idea);
        await adapter.SaveChangesAsync();
        return true;
    }

    private IdeaResponse MapToResponse(Idea idea, bool topicIsOpen) =>
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
            Links = HateoasBuilder.ForIdea(idea.Id, idea.TopicId, topicIsOpen, version)
        };
}
