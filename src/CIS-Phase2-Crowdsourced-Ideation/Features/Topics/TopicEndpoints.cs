using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

public static class TopicEndpoints
{
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/topics").WithTags("Topics").RequireAuthorization();

        group.MapGet("/", HandleGetAllTopics);
        group.MapGet("/{id}", HandleGetTopicById);

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

    internal static TopicResponse ToResponse(Topic t) =>
        new(t.Id, t.Title, t.Description, t.Status.ToString(), t.CreatedBy, t.CreatedAt, t.UpdatedAt);
}