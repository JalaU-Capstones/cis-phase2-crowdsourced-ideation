using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;

public static class StatisticsEndpoints
{
    private const int DefaultLimit = 10;

    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/statistics")
            .WithTags("Statistics");

        group.MapGet("/top-topics", HandleTopTopics)
            .WithName("GetTopTopics")
            .WithSummary("Get top topics (public)")
            .WithDescription("""
                Public endpoint.
                Returns topics ordered by total votes across all ideas in the topic (descending).
                Supports pagination via `limit` and `offset`.
                """)
            .Produces<IReadOnlyList<TopTopicDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .WithOpenApi(op =>
            {
                op.Parameters ??= new List<OpenApiParameter>();
                op.Parameters.Add(new OpenApiParameter
                {
                    Name = "limit",
                    In = ParameterLocation.Query,
                    Required = false,
                    Description = "Max number of records to return (default 10). Must be > 0.",
                    Schema = new OpenApiSchema { Type = "integer", Default = new OpenApiInteger(DefaultLimit) }
                });
                op.Parameters.Add(new OpenApiParameter
                {
                    Name = "offset",
                    In = ParameterLocation.Query,
                    Required = false,
                    Description = "Records to skip (default 0). Must be >= 0.",
                    Schema = new OpenApiSchema { Type = "integer", Default = new OpenApiInteger(0) }
                });

                if (op.Responses.TryGetValue("200", out var ok) &&
                    ok.Content.TryGetValue("application/json", out var json))
                {
                    json.Examples = new Dictionary<string, OpenApiExample>
                    {
                        ["Example"] = new()
                        {
                            Summary = "Top topics",
                            Value = new OpenApiArray
                            {
                                new OpenApiObject
                                {
                                    ["topicId"] = new OpenApiString("61cb20af-ae78-4148-a057-df5e7962db39"),
                                    ["topicTitle"] = new OpenApiString("Checkout improvements"),
                                    ["status"] = new OpenApiString("OPEN"),
                                    ["ideasCount"] = new OpenApiInteger(4),
                                    ["votesCount"] = new OpenApiInteger(12)
                                }
                            }
                        }
                    };
                }
                return op;
            });

        group.MapGet("/most-voted-ideas", HandleMostVotedIdeas)
            .WithName("GetMostVotedIdeas")
            .WithSummary("Get most voted ideas (public)")
            .WithDescription("""
                Public endpoint.
                Returns ideas ordered by vote count (descending), including the idea's topic information.
                Supports pagination via `limit` and `offset`.
                """)
            .Produces<IReadOnlyList<MostVotedIdeaDto>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .WithOpenApi(op =>
            {
                if (op.Responses.TryGetValue("200", out var ok) &&
                    ok.Content.TryGetValue("application/json", out var json))
                {
                    json.Examples = new Dictionary<string, OpenApiExample>
                    {
                        ["Example"] = new()
                        {
                            Summary = "Most voted ideas",
                            Value = new OpenApiArray
                            {
                                new OpenApiObject
                                {
                                    ["ideaId"] = new OpenApiString("d5bd9f40-9f2a-4c25-8d18-1dfc7f2d965e"),
                                    ["ideaTitle"] = new OpenApiString("Guest checkout"),
                                    ["topicId"] = new OpenApiString("61cb20af-ae78-4148-a057-df5e7962db39"),
                                    ["topicTitle"] = new OpenApiString("Checkout improvements"),
                                    ["votesCount"] = new OpenApiInteger(7)
                                }
                            }
                        }
                    };
                }
                return op;
            });

        group.MapGet("/topic/{topicId}/summary", HandleTopicSummary)
            .WithName("GetTopicSummary")
            .WithSummary("Get topic summary (public)")
            .WithDescription("""
                Public endpoint.
                Returns aggregated statistics for a specific topic:
                - ideas count
                - total votes count
                - winning idea (if any)
                - most voted idea
                """)
            .Produces<TopicSummaryDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .WithOpenApi(op =>
            {
                if (op.Responses.TryGetValue("200", out var ok) &&
                    ok.Content.TryGetValue("application/json", out var json))
                {
                    json.Examples = new Dictionary<string, OpenApiExample>
                    {
                        ["Example"] = new()
                        {
                            Summary = "Topic summary",
                            Value = new OpenApiObject
                            {
                                ["topicId"] = new OpenApiString("61cb20af-ae78-4148-a057-df5e7962db39"),
                                ["topicTitle"] = new OpenApiString("Checkout improvements"),
                                ["status"] = new OpenApiString("CLOSED"),
                                ["ideasCount"] = new OpenApiInteger(4),
                                ["votesCount"] = new OpenApiInteger(12),
                                ["winningIdea"] = new OpenApiObject
                                {
                                    ["ideaId"] = new OpenApiString("d5bd9f40-9f2a-4c25-8d18-1dfc7f2d965e"),
                                    ["ideaTitle"] = new OpenApiString("Guest checkout"),
                                    ["votesCount"] = new OpenApiInteger(7)
                                },
                                ["mostVotedIdea"] = new OpenApiObject
                                {
                                    ["ideaId"] = new OpenApiString("d5bd9f40-9f2a-4c25-8d18-1dfc7f2d965e"),
                                    ["ideaTitle"] = new OpenApiString("Guest checkout"),
                                    ["votesCount"] = new OpenApiInteger(7)
                                }
                            }
                        }
                    };
                }
                return op;
            });

        return endpoints;
    }

    public static async Task<Results<Ok<IReadOnlyList<TopTopicDto>>, BadRequest<ErrorResponse>>> HandleTopTopics(
        int? limit,
        int? offset,
        IStatisticsService service)
    {
        if (!TryValidatePaging(limit, offset, out var l, out var o, out var error))
            return TypedResults.BadRequest(new ErrorResponse(error));

        var data = await service.GetTopTopicsAsync(l, o);
        return TypedResults.Ok(data);
    }

    public static async Task<Results<Ok<IReadOnlyList<MostVotedIdeaDto>>, BadRequest<ErrorResponse>>> HandleMostVotedIdeas(
        int? limit,
        int? offset,
        IStatisticsService service)
    {
        if (!TryValidatePaging(limit, offset, out var l, out var o, out var error))
            return TypedResults.BadRequest(new ErrorResponse(error));

        var data = await service.GetMostVotedIdeasAsync(l, o);
        return TypedResults.Ok(data);
    }

    public static async Task<Results<Ok<TopicSummaryDto>, NotFound, BadRequest<ErrorResponse>>> HandleTopicSummary(
        string topicId,
        IStatisticsService service)
    {
        if (string.IsNullOrWhiteSpace(topicId))
            return TypedResults.BadRequest(new ErrorResponse("topicId is required."));

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
