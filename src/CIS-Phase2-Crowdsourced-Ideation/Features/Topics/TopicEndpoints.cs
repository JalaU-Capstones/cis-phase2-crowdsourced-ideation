using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
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
    private const string ForbiddenErrorMessage = "You are not authorized to modify this topic.";
    private const string StatusErrorMessage = "Status must be 'OPEN' or 'CLOSED'.";
    private const string UserIdErrorMessage = "User identity not found or invalid.";

    /// <summary>
    /// Maps topic endpoints to the routing system.
    /// </summary>
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/topics")
            .WithTags("Topics");

        // Public read access
        group.MapGet("/", HandleGetAllTopics)
            .WithName("GetAllTopics")
            .WithSummary("Get all topics (public)")
            .WithDescription("Public endpoint. Returns all topics.")
            .Produces<IEnumerable<TopicResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id}", HandleGetTopicById)
            .WithName("GetTopicById")
            .WithSummary("Get topic by id (public)")
            .WithDescription("Public endpoint. Returns a topic by its id.")
            .Produces<TopicResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Protected write access
        var protectedGroup = group.MapGroup("/")
            .RequireAuthorization();

        protectedGroup.MapPost("/", HandleCreateTopic)
            .WithName("CreateTopic")
            .WithSummary("Create a topic (authenticated)")
            .WithDescription("Only authenticated users can create topics. The creator becomes the owner.")
            .Produces<TopicResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapPut("/{id}", HandleUpdateTopic)
            .WithName("UpdateTopic")
            .WithSummary("Update a topic (owner only)")
            .WithDescription("Only the topic owner can update title/description/status.")
            .Produces<TopicResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapDelete("/{id}", HandleDeleteTopic)
            .WithName("DeleteTopic")
            .WithSummary("Delete a topic (owner only)")
            .WithDescription("Only the topic owner can delete. Deleting a topic cascades delete related ideas and votes.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

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
        // 1. Authentication (Checked by middleware [RequireAuthorization])
        
        // 2. Authorization (Owner validation - not applicable for create)
        var login = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(login))
            return TypedResults.Unauthorized();

        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Login == login);
        if (dbUser == null)
            return TypedResults.Unauthorized();

        // 3. Input validation
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            return TypedResults.BadRequest<object>(new { error = TitleLengthErrorMessage });

        var topic = new Topic
        {
            Id          = Guid.NewGuid().ToString(),
            Title       = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status      = TopicStatus.OPEN,
            OwnerId     = dbUser.Id,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/topics/{topic.Id}", ToResponse(topic));
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
        // 1. Authentication (Checked by middleware [RequireAuthorization])
        
        // Find topic first to check ownership
        var topic = await db.Topics.FindAsync(id);
        if (topic is null) return TypedResults.NotFound();

        // 2. Authorization (Ownership)
        var login = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(login))
            return TypedResults.Forbid();

        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Login == login);
        if (dbUser == null || topic.OwnerId != dbUser.Id)
            return TypedResults.Forbid();

        // 3. Input validation
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            return TypedResults.BadRequest<object>(new { error = TitleLengthErrorMessage });

        if (!Enum.TryParse<TopicStatus>(request.Status, ignoreCase: true, out var parsedStatus))
            return TypedResults.BadRequest<object>(new { error = StatusErrorMessage });

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
        // 1. Authentication (Checked by middleware [RequireAuthorization])
        
        // Find topic first to check ownership
        var topic = await db.Topics.FindAsync(id);
        if (topic is null) return TypedResults.NotFound();

        // 2. Authorization (Ownership)
        var login = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(login))
            return TypedResults.Forbid();

        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Login == login);
        if (dbUser == null || topic.OwnerId != dbUser.Id)
            return TypedResults.Forbid();

        // Legacy schema note:
        // The MySQL foreign keys in init.sql do NOT specify ON DELETE CASCADE for topics -> ideas.
        // To keep the intended behavior (deleting a topic removes its ideas and votes), we perform
        // an application-level cascade delete in the correct order.
        var ideaIds = await db.Ideas
            .AsNoTracking()
            .Where(i => i.TopicId == id)
            .Select(i => i.Id)
            .ToListAsync();

        if (ideaIds.Count > 0)
        {
            // Prefer server-side deletes (EF Core 7/8) when supported by provider.
            try
            {
                await db.Votes.Where(v => ideaIds.Contains(v.IdeaId)).ExecuteDeleteAsync();
                await db.Ideas.Where(i => i.TopicId == id).ExecuteDeleteAsync();
            }
            catch (NotSupportedException)
            {
                var votes = await db.Votes.Where(v => ideaIds.Contains(v.IdeaId)).ToListAsync();
                db.Votes.RemoveRange(votes);

                var ideas = await db.Ideas.Where(i => i.TopicId == id).ToListAsync();
                db.Ideas.RemoveRange(ideas);
            }
        }

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
