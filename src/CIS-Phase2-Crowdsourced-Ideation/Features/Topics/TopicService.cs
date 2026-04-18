using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using System.Security.Claims;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

public interface ITopicService
{
    Task<PagedResponse<TopicResponse>> GetAllTopicsAsync(int? page, int? size, string? status, string? ownerId, string? sortBy, string? order);
    Task<TopicResponse?> GetTopicByIdAsync(string id);
    Task<TopicResponse> CreateTopicAsync(CreateTopicRequest request, ClaimsPrincipal user);
    Task<TopicResponse?> UpdateTopicAsync(string id, UpdateTopicRequest request, ClaimsPrincipal user);
    Task<bool> DeleteTopicAsync(string id, ClaimsPrincipal user);
}

public class TopicService(IRepositoryAdapter adapter, string version = "v1") : ITopicService
{
    private static readonly string[] ValidSortFields = ["createdAt", "title", "updatedAt"];
    private static readonly string[] ValidOrders = ["asc", "desc"];

    private async Task<string> ResolveUserIdAsync(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(raw))
            throw new UnauthorizedAccessException("User identity not found or invalid");

        // Tests and some JWT setups provide the user id directly as a GUID.
        if (Guid.TryParse(raw, out var userId))
            return userId.ToString();

        // Phase 1 integration typically provides login/subject; map to DB user record to get the GUID id.
        var dbUser = await adapter.Users.GetByLoginAsync(raw);
        if (dbUser == null || !Guid.TryParse(dbUser.Id, out userId))
            throw new UnauthorizedAccessException("User identity not found or invalid");

        return userId.ToString();
    }

    public async Task<PagedResponse<TopicResponse>> GetAllTopicsAsync(int? page, int? size, string? status, string? ownerId, string? sortBy, string? order)
    {
        // 1. Validate pagination
        var currentPage = page ?? 0;
        var pageSize = size ?? 10;

        if (currentPage < 0)
            throw new ArgumentException("page must be >= 0.");
        if (pageSize <= 0)
            throw new ArgumentException("size must be >= 1.");

        // 2. Validate status
        if (!string.IsNullOrEmpty(status) && !Enum.TryParse<TopicStatus>(status, ignoreCase: true, out _))
            throw new ArgumentException("status must be 'OPEN' or 'CLOSED'.");

        // 3. Validate sorting
        var sortField = sortBy ?? "createdAt";
        var sortOrder = order ?? "desc";

        if (!ValidSortFields.Contains(sortField))
            throw new ArgumentException($"sortBy must be one of: {string.Join(", ", ValidSortFields)}.");
        if (!ValidOrders.Contains(sortOrder))
            throw new ArgumentException($"order must be 'asc' or 'desc'.");

        // Get filtered topics
        var topics = await adapter.Topics.GetFilteredAsync(status, ownerId);

        // Sort in memory for simplicity
        var sortedTopics = (sortField, sortOrder) switch
        {
            ("title", "asc") => topics.OrderBy(t => t.Title),
            ("title", "desc") => topics.OrderByDescending(t => t.Title),
            ("updatedAt", "asc") => topics.OrderBy(t => t.UpdatedAt),
            ("updatedAt", "desc") => topics.OrderByDescending(t => t.UpdatedAt),
            ("createdAt", "asc") => topics.OrderBy(t => t.CreatedAt),
            _ => topics.OrderByDescending(t => t.CreatedAt),
        };

        // Paginate
        var totalItems = sortedTopics.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var pagedTopics = sortedTopics.Skip(currentPage * pageSize).Take(pageSize).ToList();

        // Winning ideas for closed topics
        var closedTopicIds = pagedTopics.Where(t => t.Status == TopicStatus.CLOSED).Select(t => t.Id).ToList();
        Dictionary<string, WinningIdeaResponse?> winners = new();
        if (closedTopicIds.Any())
        {
            foreach (var tid in closedTopicIds)
            {
                var ideas = await adapter.Ideas.GetByTopicIdAsync(tid);
                var winner = ideas.FirstOrDefault(i => i.IsWinning);
                if (winner != null)
                    winners[tid] = MapToWinningIdeaResponse(winner);
            }
        }

        var data = pagedTopics.Select(t => ToResponse(t, t.Status == TopicStatus.CLOSED ? winners.GetValueOrDefault(t.Id) : null));

        return new PagedResponse<TopicResponse>(data, currentPage, pageSize, totalItems, totalPages);
    }

    public async Task<TopicResponse?> GetTopicByIdAsync(string id)
    {
        var topic = await adapter.Topics.GetByIdAsync(id);
        if (topic == null) return null;

        WinningIdeaResponse? winner = null;
        if (topic.Status == TopicStatus.CLOSED)
        {
            var ideas = await adapter.Ideas.GetByTopicIdAsync(id);
            var winningIdea = ideas.FirstOrDefault(i => i.IsWinning);
            if (winningIdea != null)
                winner = MapToWinningIdeaResponse(winningIdea);
        }

        return ToResponse(topic, winner);
    }

    public async Task<TopicResponse> CreateTopicAsync(CreateTopicRequest request, ClaimsPrincipal user)
    {
        var userId = await ResolveUserIdAsync(user);

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            throw new ArgumentException("Title is required and must be at most 200 characters.");

        var topic = new Topic
        {
            Id = Guid.NewGuid().ToString(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = TopicStatus.OPEN,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await adapter.Topics.AddAsync(topic);
        await adapter.SaveChangesAsync();

        return ToResponse(topic);
    }

    public async Task<TopicResponse?> UpdateTopicAsync(string id, UpdateTopicRequest request, ClaimsPrincipal user)
    {
        var topic = await adapter.Topics.GetByIdAsync(id);
        if (topic == null) return null;

        var userId = await ResolveUserIdAsync(user);
        if (topic.OwnerId != userId)
            throw new UnauthorizedAccessException("You are not authorized to modify this topic.");

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            throw new ArgumentException("Title is required and must be at most 200 characters.");

        if (!Enum.TryParse<TopicStatus>(request.Status, ignoreCase: true, out var parsedStatus))
            throw new ArgumentException("Status must be 'OPEN' or 'CLOSED'.");

        if (topic.Status == TopicStatus.CLOSED && parsedStatus == TopicStatus.OPEN)
            throw new ArgumentException("This topic is closed and cannot be reopened.");

        var wasOpen = topic.Status == TopicStatus.OPEN;

        topic.Title = request.Title.Trim();
        topic.Description = request.Description?.Trim();
        topic.Status = parsedStatus;
        topic.UpdatedAt = DateTime.UtcNow;

        WinningIdeaResponse? winningIdea = null;
        if (wasOpen && parsedStatus == TopicStatus.CLOSED)
        {
            winningIdea = await MarkWinningIdeaAsync(id);
        }

        await adapter.Topics.UpdateAsync(topic);
        await adapter.SaveChangesAsync();

        return ToResponse(topic, parsedStatus == TopicStatus.CLOSED ? winningIdea : null);
    }

    public async Task<bool> DeleteTopicAsync(string id, ClaimsPrincipal user)
    {
        var topic = await adapter.Topics.GetByIdAsync(id);
        if (topic == null) return false;

        var userId = await ResolveUserIdAsync(user);
        if (topic.OwnerId != userId)
            throw new UnauthorizedAccessException("You are not authorized to modify this topic.");

        // Cascade delete
        var ideas = await adapter.Ideas.GetByTopicIdAsync(id);
        foreach (var idea in ideas)
        {
            var votes = await adapter.Votes.GetByIdeaIdAsync(idea.Id);
            foreach (var vote in votes)
            {
                await adapter.Votes.DeleteAsync(vote);
            }
            await adapter.Ideas.DeleteAsync(idea);
        }

        await adapter.Topics.DeleteAsync(topic);
        await adapter.SaveChangesAsync();
        return true;
    }

    private async Task<WinningIdeaResponse?> MarkWinningIdeaAsync(string topicId)
    {
        var ideas = await adapter.Ideas.GetByTopicIdAsync(topicId);
        if (!ideas.Any()) return null;

        var ideaIds = ideas.Select(i => i.Id).ToList();
        var voteCounts = new Dictionary<Guid, int>();
        foreach (var iid in ideaIds)
        {
            voteCounts[iid] = await adapter.Votes.CountByIdeaIdAsync(iid);
        }

        var winner = ideas
            .OrderByDescending(i => voteCounts.GetValueOrDefault(i.Id, 0))
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .First();

        foreach (var idea in ideas)
        {
            idea.IsWinning = idea.Id == winner.Id;
            await adapter.Ideas.UpdateAsync(idea);
        }

        return MapToWinningIdeaResponse(winner);
    }

    private TopicResponse ToResponse(Topic t, WinningIdeaResponse? winningIdea = null) =>
        new(t.Id, t.Title, t.Description, t.Status.ToString(), t.OwnerId, t.CreatedAt, t.UpdatedAt, winningIdea)
        {
            Links = HateoasBuilder.ForTopic(t.Id, t.Status.ToString(), version)
        };

    private static WinningIdeaResponse MapToWinningIdeaResponse(Idea idea) =>
        new(idea.Id, idea.TopicId, idea.OwnerId, idea.Title, idea.Description, idea.CreatedAt, idea.UpdatedAt, idea.IsWinning);
}
