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
        var group = endpoints.MapGroup("/api/topics")
            .WithTags("Topics");

        // Public read access
group.MapGet("/", (AppDbContext db,
    [FromQuery] int? page, [FromQuery] int? size,
    [FromQuery] string? status, [FromQuery] string? ownerId,
    [FromQuery] string? sortBy, [FromQuery] string? order) =>
    HandleGetAllTopics(db, page, size, status, ownerId, sortBy, order))
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

        protectedGroup.MapPut("/{id}", UpdateTopicWithInfoHeader)
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
    AppDbContext db,
    int? page, int? size,
    string? status, string? ownerId,
    string? sortBy, string? order)
{
    // 1. Validar paginación
    var currentPage = page ?? 0;
    var pageSize    = size ?? 10;

    if (currentPage < 0)
        return TypedResults.BadRequest<object>(new { error = "page must be >= 0." });
    if (pageSize <= 0)
        return TypedResults.BadRequest<object>(new { error = "size must be >= 1." });

    // 2. Validar sorting
    var sortField = sortBy ?? "createdAt";
    var sortOrder = order  ?? "desc";

    if (!ValidSortFields.Contains(sortField))
    return TypedResults.BadRequest<object>(new { error = $"sortBy must be one of: {string.Join(", ", ValidSortFields)}." });
    if (!ValidOrders.Contains(sortOrder))
    return TypedResults.BadRequest<object>(new { error = $"order must be 'asc' or 'desc'." });

    // 3. Validar filtering
    TopicStatus? parsedStatus = null;
    if (status is not null)
    {
        if (!Enum.TryParse<TopicStatus>(status, ignoreCase: true, out var s))
            return TypedResults.BadRequest<object>(new { error = "status must be 'OPEN' or 'CLOSED'." });
        parsedStatus = s;
    }

    // 4. Query base
    var query = db.Topics.AsNoTracking().AsQueryable();

    // 5. Aplicar filtros
    if (parsedStatus is not null)
        query = query.Where(t => t.Status == parsedStatus.Value);
    if (!string.IsNullOrWhiteSpace(ownerId))
        query = query.Where(t => t.OwnerId == ownerId);

    // 6. Aplicar sorting
    query = (sortField, sortOrder) switch
    {
        ("title",     "asc")  => query.OrderBy(t => t.Title),
        ("title",     "desc") => query.OrderByDescending(t => t.Title),
        ("updatedAt", "asc")  => query.OrderBy(t => t.UpdatedAt),
        ("updatedAt", "desc") => query.OrderByDescending(t => t.UpdatedAt),
        ("createdAt", "asc")  => query.OrderBy(t => t.CreatedAt),
        _                     => query.OrderByDescending(t => t.CreatedAt),
    };

    // 7. Contar total ANTES de paginar
    var totalItems = await query.CountAsync();
    var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

    // 8. Aplicar paginación
    var topics = await query
        .Skip(currentPage * pageSize)
        .Take(pageSize)
        .ToListAsync();

    // 9. Winning idea para topics CLOSED (lógica de main)
    var closedTopicIds = topics
        .Where(t => t.Status == TopicStatus.CLOSED)
        .Select(t => t.Id)
        .ToList();

    Dictionary<string, WinningIdeaResponse?> winnersByTopicId = new();
    if (closedTopicIds.Count > 0)
    {
        var ideasForClosedTopics = await db.Ideas
            .AsNoTracking()
            .Where(i => closedTopicIds.Contains(i.TopicId))
            .ToListAsync();

        winnersByTopicId = ideasForClosedTopics
            .Where(i => i.IsWinning)
            .GroupBy(i => i.TopicId)
            .ToDictionary(
                g => g.Key,
                g => (WinningIdeaResponse?)MapToWinningIdeaResponse(g.First()));
    }

    var response = new PagedResponse<TopicResponse>(
        Data:        topics.Select(t =>
            ToResponse(t, t.Status == TopicStatus.CLOSED
                ? winnersByTopicId.GetValueOrDefault(t.Id)
                : null)),
        CurrentPage: currentPage,
        PageSize:    pageSize,
        TotalItems:  totalItems,
        TotalPages:  totalPages
    );

    return TypedResults.Ok(response);
}

    /// <summary>
    /// Retrieves a topic by its unique identifier.
    /// </summary>
    public static async Task<Results<Ok<TopicResponse>, NotFound>> HandleGetTopicById(string id, AppDbContext db)
    {
        var topic = await db.Topics.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (topic is null)
            return TypedResults.NotFound();

        WinningIdeaResponse? winner = null;
        if (topic.Status == TopicStatus.CLOSED)
        {
            var ideas = await db.Ideas
                .AsNoTracking()
                .Where(i => i.TopicId == id)
                .ToListAsync();

            var winningIdea = ideas.FirstOrDefault(i => i.IsWinning);
            if (winningIdea is not null)
                winner = MapToWinningIdeaResponse(winningIdea);
        }

        return TypedResults.Ok(ToResponse(topic, winner));
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

        // Business rule: once CLOSED, a topic cannot be reopened.
        if (topic.Status == TopicStatus.CLOSED && parsedStatus == TopicStatus.OPEN)
            return TypedResults.BadRequest<object>(new { error = TopicCannotBeReopenedMessage });

        var wasOpen = topic.Status == TopicStatus.OPEN;

        topic.Title = request.Title.Trim();
        topic.Description = request.Description?.Trim();
        topic.Status = parsedStatus;
        topic.UpdatedAt = DateTime.UtcNow;

        WinningIdeaResponse? winningIdea = null;
        if (wasOpen && parsedStatus == TopicStatus.CLOSED)
        {
            winningIdea = await MarkWinningIdeaAsync(db, topicId: topic.Id);
        }

        await db.SaveChangesAsync();
        return TypedResults.Ok(ToResponse(topic, parsedStatus == TopicStatus.CLOSED ? winningIdea : null));
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

    private static async Task<Results<Ok<TopicResponse>, NotFound, BadRequest<object>, ForbidHttpResult>> UpdateTopicWithInfoHeader(
        string id,
        UpdateTopicRequest request,
        ClaimsPrincipal user,
        AppDbContext db,
        HttpContext http)
    {
        var previousStatus = await db.Topics
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => t.Status)
            .FirstOrDefaultAsync();

        var result = await HandleUpdateTopic(id, request, user, db);

        if (result.Result is Ok<TopicResponse> ok &&
            previousStatus == TopicStatus.OPEN &&
            string.Equals(ok.Value?.Status, TopicStatus.CLOSED.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            http.Response.Headers["X-Info"] = "Topic closed. Once closed, it cannot be reopened.";
        }

        return result;
    }

    /// <summary>
    /// Deletes a topic. Only the owner can perform this action.
    /// </summary>
    public static async Task<Results<Ok<object>, NotFound, ForbidHttpResult>> HandleDeleteTopic(
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
            // Prefer server-side deletes (EF Core 7/8) on relational providers only.
            if (!db.Database.IsRelational())
            {
                var votes = await db.Votes.Where(v => ideaIds.Contains(v.IdeaId)).ToListAsync();
                db.Votes.RemoveRange(votes);

                var ideas = await db.Ideas.Where(i => i.TopicId == id).ToListAsync();
                db.Ideas.RemoveRange(ideas);
            }
            else
            {
                await db.Votes.Where(v => ideaIds.Contains(v.IdeaId)).ExecuteDeleteAsync();
                await db.Ideas.Where(i => i.TopicId == id).ExecuteDeleteAsync();
            }
        }

        db.Topics.Remove(topic);
        await db.SaveChangesAsync();
        return TypedResults.Ok<object>(new
        {
            message = "Topic deleted. This action also deleted all related ideas and votes.",
            topicId = id
        });
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