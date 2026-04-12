using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Features;
using Microsoft.AspNetCore.Mvc;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public static class IdeaEndpoints
{
    public static void MapIdeaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ideas")
            .WithTags("Ideas");

        group.MapGet("/", GetAllIdeas)
            .WithName("GetAllIdeas")
            .WithSummary("Get all ideas (public)")
            .WithDescription("Public endpoint. Returns paginated ideas with sorting support.")
            .Produces<PagedResponse<IdeaResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetIdea)
            .WithName("GetIdea")
            .WithSummary("Get an idea by its ID")
            .WithDescription("Public endpoint. Returns an idea by id.")
            .Produces<IdeaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/topic/{topicId}", GetIdeasByTopic)
            .WithName("GetIdeasByTopic")
            .WithSummary("Get all ideas for a specific topic")
            .WithDescription("""
                Public endpoint. Returns all ideas for the given topic id.
                Returns an empty array if the topic has no ideas or the topic does not exist.
                """)
            .Produces<IEnumerable<IdeaResponse>>(StatusCodes.Status200OK)
            .WithOpenApi(operation =>
            {
                var topicIdParam = operation.Parameters.FirstOrDefault(p => p.Name == "topicId");
                if (topicIdParam is not null)
                {
                    topicIdParam.Description = "Topic id (UUID). If it does not exist, the endpoint returns an empty array.";
                    topicIdParam.Example = new OpenApiString("61cb20af-ae78-4148-a057-df5e7962db39");
                }

                if (operation.Responses.TryGetValue("200", out var ok) &&
                    ok.Content.TryGetValue("application/json", out var json))
                {
                    json.Examples = new Dictionary<string, OpenApiExample>
                    {
                        ["IdeasFound"] = new()
                        {
                            Summary = "Ideas for an existing topic",
                            Value = new OpenApiArray
                            {
                                new OpenApiObject
                                {
                                    ["id"] = new OpenApiString("d5bd9f40-9f2a-4c25-8d18-1dfc7f2d965e"),
                                    ["topicId"] = new OpenApiString("61cb20af-ae78-4148-a057-df5e7962db39"),
                                    ["ownerId"] = new OpenApiString("4aa1fdf5-fb0a-4a79-a0a4-2c5be62da24a"),
                                    ["title"] = new OpenApiString("Reduce checkout friction"),
                                    ["description"] = new OpenApiString("Offer guest checkout and fewer required fields."),
                                    ["createdAt"] = new OpenApiString("2026-04-09T12:00:00Z"),
                                    ["updatedAt"] = new OpenApiString("2026-04-09T12:00:00Z"),
                                    ["isWinning"] = new OpenApiBoolean(false)
                                }
                            }
                        },
                        ["NoIdeas"] = new()
                        {
                            Summary = "No ideas (topic missing or has none)",
                            Value = new OpenApiArray()
                        }
                    };
                }

                return operation;
            });

        // Protected write access
        var protectedGroup = group.MapGroup("/")
            .RequireAuthorization();

        protectedGroup.MapPost("/", CreateIdea)
            .WithName("CreateIdea")
            .WithSummary("Create a new idea for a specific topic")
            .WithDescription("""
                Any authenticated user can create an idea for an existing topic.
                The idea is stored in the legacy-compatible `ideas.content` column as JSON.
                """)
            .Produces<IdeaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapPut("/{id:guid}", UpdateIdea)
            .WithName("UpdateIdea")
            .WithSummary("Update an existing idea")
            .WithDescription("""
                Only the owner of the idea can update it.
                Ideas cannot be modified when the associated topic is CLOSED.
                """)
            .Produces<IdeaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapDelete("/{id:guid}", DeleteIdea)
            .WithName("DeleteIdea")
            .WithSummary("Delete an idea")
            .WithDescription("""
                Only the owner of the idea can delete it.
                Ideas cannot be deleted when the associated topic is CLOSED.
                Deleting an idea will also delete all related votes.
                Deleting a topic will delete all related ideas and votes.
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateIdea(CreateIdeaRequest request, IIdeaService service, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.TopicId) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return TypedResults.BadRequest("Required fields are missing");
        }

        try 
        {
            var result = await service.CreateIdeaAsync(request, user);
            return TypedResults.Created($"/api/ideas/{result.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> GetAllIdeas(
        IIdeaService service,
        [FromQuery] int? page,
        [FromQuery] int? size,
        [FromQuery] string? sortBy,
        [FromQuery] string? order)
    {
        try
        {
            var result = await service.GetAllIdeasAsync(page, size, sortBy, order);
            return TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetIdea(Guid id, IIdeaService service)
    {
        var result = await service.GetIdeaByIdAsync(id);
        return result == null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    private static async Task<IResult> GetIdeasByTopic(string topicId, IIdeaService service)
    {
        var result = await service.GetIdeasByTopicIdAsync(topicId);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> UpdateIdea(Guid id, UpdateIdeaRequest request, IIdeaService service, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return TypedResults.BadRequest("Required fields are missing");
        }

        try
        {
            var result = await service.UpdateIdeaAsync(id, request, user);
            return result == null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> DeleteIdea(Guid id, IIdeaService service, ClaimsPrincipal user)
    {
        try
        {
            var result = await service.DeleteIdeaAsync(id, user);
            return result
                ? TypedResults.Ok(new { message = "Idea deleted. All votes related to this idea were deleted as well.", ideaId = id })
                : TypedResults.NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
