using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

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
    private const string TopicCannotBeReopenedMessage = "This topic is closed and cannot be reopened.";
    private const string UserIdErrorMessage = "User identity not found or invalid.";
    private static readonly string[] ValidSortFields = ["createdAt", "title", "updatedAt"];
    private static readonly string[] ValidOrders = ["asc", "desc"];

    /// <summary>
    /// Maps topic endpoints to the routing system.
    /// </summary>
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/topics")
            .WithTags("Topics");

        // Public read access
        group.MapGet("/", HandleGetAllTopics)
            .WithName("GetAllTopics")
            .WithSummary("Get all topics (public)")
            .WithDescription("Public endpoint. Returns paginated topics with filtering and sorting support.")
            .Produces<PagedResponse<TopicResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", HandleGetTopicById)
            .WithName("GetTopicById")
            .WithSummary("Get topic by id (public)")
            .WithDescription("""
                Public endpoint. Returns a topic by its id.
                When a topic is CLOSED, the response includes the winning idea (the idea with IsWinning=true), if present.
                """)
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
            .WithDescription("""
                Only the topic owner can update title/description/status.
                Once a topic is CLOSED, it cannot be reopened (status cannot be changed back to OPEN).
                """)
            .Produces<TopicResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapDelete("/{id}", HandleDeleteTopic)
            .WithName("DeleteTopic")
            .WithSummary("Delete a topic (owner only)")
            .WithDescription("Only the topic owner can delete. Deleting a topic cascades delete related ideas and votes.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>
    /// Retrieves all topics.
    /// </summary>
   public static async Task<IResult> HandleGetAllTopics(
    ITopicService service,
    [FromQuery] int? page, [FromQuery] int? size,
    [FromQuery] string? status, [FromQuery] string? ownerId,
    [FromQuery] string? sortBy, [FromQuery] string? order)
    {
        try
        {
            var response = await service.GetAllTopicsAsync(page, size, status, ownerId, sortBy, order);
            return TypedResults.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves a topic by its unique identifier.
    /// </summary>
    public static async Task<Results<Ok<TopicResponse>, NotFound>> HandleGetTopicById(string id, ITopicService service)
    {
        var topic = await service.GetTopicByIdAsync(id);
        if (topic is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(topic);
    }

    /// <summary>
    /// Creates a new topic.
    /// </summary>
    public static async Task<Results<Created<TopicResponse>, BadRequest<object>, UnauthorizedHttpResult>> HandleCreateTopic(
        CreateTopicRequest request,
        ClaimsPrincipal user,
        ITopicService service)
    {
        try
        {
            var topic = await service.CreateTopicAsync(request, user);
            return TypedResults.Created($"/api/v1/topics/{topic.Id}", topic);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Unauthorized();
        }
    }

    /// <summary>
    /// Updates an existing topic. Only the owner can perform this action.
    /// </summary>
    public static async Task<Results<Ok<TopicResponse>, NotFound, BadRequest<object>, ForbidHttpResult>> HandleUpdateTopic(
        string id,
        UpdateTopicRequest request,
        ClaimsPrincipal user,
        ITopicService service,
        HttpContext http)
    {
        try
        {
            var topic = await service.UpdateTopicAsync(id, request, user);
            if (topic == null) return TypedResults.NotFound();

            if (string.Equals(request.Status, TopicStatus.CLOSED.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                http.Response.Headers["X-Info"] = "Topic closed. Once closed, it cannot be reopened.";
            }

            return TypedResults.Ok(topic);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Forbid();
        }
    }

    private static async Task<WinningIdeaResponse?> MarkWinningIdeaAsync(AppDbContext db, string topicId)
    {
        // Winning idea rule (US 2.2):
        // When a topic is CLOSED, set IsWinning=true for the idea with the most votes.
        // Since IsWinning is stored inside ideas.content JSON, we must materialize ideas first.
        var ideas = await db.Ideas
            .Where(i => i.TopicId == topicId)
            .ToListAsync();

        if (ideas.Count == 0)
            return null;

        var ideaIds = ideas.Select(i => i.Id).ToList();

        var counts = await db.Votes
            .AsNoTracking()
            .Where(v => ideaIds.Contains(v.IdeaId))
            .GroupBy(v => v.IdeaId)
            .Select(g => new { IdeaId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countsByIdeaId = counts.ToDictionary(x => x.IdeaId, x => x.Count);

        var winner = ideas
            .OrderByDescending(i => countsByIdeaId.GetValueOrDefault(i.Id, 0))
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .First();

        foreach (var idea in ideas)
        {
            idea.IsWinning = idea.Id == winner.Id;
        }

        return MapToWinningIdeaResponse(winner);
    }


    /// <summary>
    /// Deletes a topic. Only the owner can perform this action.
    /// </summary>
    public static async Task<Results<Ok<object>, NotFound, ForbidHttpResult>> HandleDeleteTopic(
        string id,
        ClaimsPrincipal user,
        ITopicService service)
    {
        try
        {
            var success = await service.DeleteTopicAsync(id, user);
            if (!success) return TypedResults.NotFound();

            return TypedResults.Ok<object>(new
            {
                message = "Topic deleted. This action also deleted all related ideas and votes.",
                topicId = id
            });
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Forbid();
        }
    }

    /// <summary>
    /// Converts a <see cref="Topic"/> entity to a <see cref="TopicResponse"/> DTO.
    /// Includes HATEOAS links (US 3.2).
    /// </summary>
    internal static TopicResponse ToResponse(Topic t, WinningIdeaResponse? winningIdea = null) =>
        new(t.Id, t.Title, t.Description, t.Status.ToString(), t.OwnerId, t.CreatedAt, t.UpdatedAt, winningIdea)
        {
            Links = HateoasBuilder.ForTopic(t.Id, t.Status.ToString())
        };

    private static WinningIdeaResponse MapToWinningIdeaResponse(Idea idea) =>
        new(idea.Id, idea.TopicId, idea.OwnerId, idea.Title, idea.Description, idea.CreatedAt, idea.UpdatedAt, idea.IsWinning);
}