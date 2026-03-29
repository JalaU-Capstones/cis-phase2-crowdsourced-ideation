using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

public static class TopicEndpoints
{
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/topics").WithTags("Topics").RequireAuthorization();

        group.MapGet("/", HandleGetAllTopics);
        group.MapGet("/{id}", HandleGetTopicById);
        group.MapPost("/", HandleCreateTopic);

        return endpoints;
    }

    public static async Task<Ok<IEnumerable<TopicResponse>>> HandleGetAllTopics(AppDbContext db)
    {
        var topics = await db.Topics.AsNoTracking().ToListAsync();
        return TypedResults.Ok(topics.Select(ToResponse));
    }

    public static async Task<Results<Ok<TopicResponse>, NotFound>> HandleGetTopicById(string id, AppDbContext db)
    {
        var topic = await db.Topics.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        return topic is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(topic));
    }

    public static async Task<Results<Created<TopicResponse>, BadRequest<object>>> HandleCreateTopic(
        CreateTopicRequest request,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            return TypedResults.BadRequest<object>(new { error = "Title is required and must be at most 200 characters." });

        var userId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return TypedResults.BadRequest<object>(new { error = "User identity not found." });

        var topic = new Topic
        {
            Id = Guid.NewGuid().ToString(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = TopicStatus.OPEN,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/topics/{topic.Id}", ToResponse(topic));
    }

    internal static TopicResponse ToResponse(Topic t) =>
        new(t.Id, t.Title, t.Description, t.Status.ToString(), t.CreatedBy, t.CreatedAt, t.UpdatedAt);
}