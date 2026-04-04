using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

/// <summary>
/// Defines endpoints for managing topics.
/// </summary>
public static class TopicEndpoints
{
    private const string TitleLengthErrorMessage = "Title is required and must be at most 200 characters.";
    private const string TopicNotFoundErrorMessage = "Topic not found.";
    private const string ForbiddenErrorMessage = "You do not have permission to modify this topic.";
    private const string StatusErrorMessage = "Status must be 'OPEN' or 'CLOSED'.";
    private const string UserIdErrorMessage = "User identity not found or invalid.";

    /// <summary>
    /// Maps topic endpoints to the routing system.
    /// </summary>
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/topics")
            .WithTags("Topics");

        // Public read access
        group.MapGet("/", HandleGetAllTopics);
        group.MapGet("/{id}", HandleGetTopicById);

        // Protected write access
        var protectedGroup = group.MapGroup("/")
            .RequireAuthorization();

        protectedGroup.MapPost("/", HandleCreateTopic);
        protectedGroup.MapPut("/{id}", HandleUpdateTopic);
        protectedGroup.MapDelete("/{id}", HandleDeleteTopic);

        return endpoints;
    }

    /// <summary>
    /// Retrieves all topics.
    /// </summary>
    public static async Task<Ok<IEnumerable<TopicResponse>>> HandleGetAllTopics(AppDbContext db)
    {
        var topics = await db.Topics.AsNoTracking().ToListAsync();
        return TypedResults.Ok(topics.Select(ToResponse));
    }

    /// <summary>
    /// Retrieves a topic by its unique identifier.
    /// </summary>
    public static async Task<Results<Ok<TopicResponse>, NotFound>> HandleGetTopicById(string id, AppDbContext db)
    {
        var topic = await db.Topics.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        return topic is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(topic));
    }

    /// <summary>
    /// Creates a new topic.
    /// </summary>
    public static async Task<Results<Created<TopicResponse>, BadRequest<object>, UnauthorizedHttpResult>> HandleCreateTopic(
        CreateTopicRequest request,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            return TypedResults.BadRequest<object>(new { error = TitleLengthErrorMessage });

        var userIdString = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString))
            return TypedResults.BadRequest<object>(new { error = UserIdErrorMessage });

        // Phase 1 User Management API uses usernames or strings as subject.
        // If OwnerId in database is Guid, we need to handle it.
        // For this fix, we assume we might need to parse it if it's a Guid string,
        // but the error description says "owner can edit/delete" logic must remain.
        Guid ownerId;
        if (!Guid.TryParse(userIdString, out ownerId))
        {
             // Fallback or handle if sub is not a Guid (e.g. it's a username)
             // For now, let's keep the Guid requirement if that's what the DB expects.
             return TypedResults.BadRequest<object>(new { error = UserIdErrorMessage });
        }

        var topic = new Topic
        {
            Id          = Guid.NewGuid().ToString(),
            Title       = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status      = TopicStatus.OPEN,
            OwnerId     = ownerId,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/topics/{topic.Id}", ToResponse(topic));
    }

    /// <summary>
    /// Updates an existing topic. Only the owner can perform this action.
    /// </summary>
    public static async Task<Results<Ok<TopicResponse>, NotFound, BadRequest<object>, ForbidHttpResult>> HandleUpdateTopic(
        string id,
        UpdateTopicRequest request,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            return TypedResults.BadRequest<object>(new { error = TitleLengthErrorMessage });

        if (!Enum.TryParse<TopicStatus>(request.Status, ignoreCase: true, out var parsedStatus))
            return TypedResults.BadRequest<object>(new { error = StatusErrorMessage });

        var topic = await db.Topics.FindAsync(id);
        if (topic is null) return TypedResults.NotFound();

        var userIdString = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId) || topic.OwnerId != currentUserId)
            return TypedResults.Forbid();

        topic.Title = request.Title.Trim();
        topic.Description = request.Description?.Trim();
        topic.Status = parsedStatus;
        topic.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return TypedResults.Ok(ToResponse(topic));
    }

    /// <summary>
    /// Deletes a topic. Only the owner can perform this action.
    /// </summary>
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult>> HandleDeleteTopic(
        string id,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var topic = await db.Topics.FindAsync(id);
        if (topic is null) return TypedResults.NotFound();

        var userIdString = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var currentUserId) || topic.OwnerId != currentUserId)
            return TypedResults.Forbid();

        db.Topics.Remove(topic);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Converts a <see cref="Topic"/> entity to a <see cref="TopicResponse"/> DTO.
    /// </summary>
    internal static TopicResponse ToResponse(Topic t) =>
        new(t.Id, t.Title, t.Description, t.Status.ToString(), t.OwnerId, t.CreatedAt, t.UpdatedAt);
}