using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using Microsoft.AspNetCore.Mvc;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;

public static class StatisticsEndpoints
{
    private const int DefaultLimit = 10;

    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder endpoints, string version = "v1")
    {
        var group = endpoints.MapGroup($"/{version}/statistics")
            .WithTags("Statistics");

        // Use version-specific adapter
        group.AddEndpointFilter(async (context, next) =>
        {
            var adapter = version == "v2" 
                ? (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MongoDbAdapter>()
                : (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MySqlAdapter>();
            
            context.HttpContext.Items["RepositoryAdapter"] = adapter;
            return await next(context);
        });

        group.MapGet("/top-topics", HandleTopTopics)
            .WithName($"GetTopTopics_{version}")
            .WithSummary($"Get top topics ({version})")
            .Produces<IReadOnlyList<TopTopicDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/most-voted-ideas", HandleMostVotedIdeas)
            .WithName($"GetMostVotedIdeas_{version}")
            .WithSummary($"Get most voted ideas ({version})")
            .Produces<IReadOnlyList<MostVotedIdeaDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/topic/{topicId}/summary", HandleTopicSummary)
            .WithName($"GetTopicSummary_{version}")
            .WithSummary($"Get topic summary ({version})")
            .Produces<TopicSummaryDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static IStatisticsService GetService(HttpContext http, string version)
    {
        var adapter = (IRepositoryAdapter)http.Items["RepositoryAdapter"]!;
        return new StatisticsService(adapter, version);
    }

    public static async Task<IResult> HandleTopTopics(
        HttpContext http,
        string version,
        int? limit,
        int? offset)
    {
        if (!TryValidatePaging(limit, offset, out var l, out var o, out var error))
            return TypedResults.BadRequest(new ErrorResponse(error));

        var service = GetService(http, version);
        var data = await service.GetTopTopicsAsync(l, o);
        return TypedResults.Ok(data);
    }

    public static async Task<IResult> HandleMostVotedIdeas(
        HttpContext http,
        string version,
        int? limit,
        int? offset)
    {
        if (!TryValidatePaging(limit, offset, out var l, out var o, out var error))
            return TypedResults.BadRequest(new ErrorResponse(error));

        var service = GetService(http, version);
        var data = await service.GetMostVotedIdeasAsync(l, o);
        return TypedResults.Ok(data);
    }

    public static async Task<IResult> HandleTopicSummary(
        string topicId,
        HttpContext http,
        string version)
    {
        if (string.IsNullOrWhiteSpace(topicId))
            return TypedResults.BadRequest(new ErrorResponse("topicId is required."));

        var service = GetService(http, version);
        var summary = await service.GetTopicSummaryAsync(topicId);
        return summary is null ? TypedResults.NotFound() : TypedResults.Ok(summary);
    }

    private static bool TryValidatePaging(int? limit, int? offset, out int validatedLimit, out int validatedOffset, out string error)
    {
        validatedLimit = limit ?? DefaultLimit;
        validatedOffset = offset ?? 0;
        error = string.Empty;

        if (validatedLimit <= 0)
        {
            error = "limit must be greater than 0.";
            return false;
        }

        if (validatedOffset < 0)
        {
            error = "offset must be greater than or equal to 0.";
            return false;
        }

        return true;
    }
}
