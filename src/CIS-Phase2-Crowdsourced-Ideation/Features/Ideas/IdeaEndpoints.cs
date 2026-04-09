using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public static class IdeaEndpoints
{
    public static void MapIdeaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ideas")
            .WithTags("Ideas")
            .RequireAuthorization();

        group.MapPost("/", CreateIdea)
            .WithName("CreateIdea")
            .WithSummary("Create a new idea for a specific topic")
            .WithDescription("Any authenticated user can create an idea.")
            .Produces<IdeaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetIdea)
            .WithName("GetIdea")
            .WithSummary("Get an idea by its ID")
            .Produces<IdeaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/topic/{topicId}", GetIdeasByTopic)
            .WithName("GetIdeasByTopic")
            .WithSummary("Get all ideas for a specific topic")
            .Produces<IEnumerable<IdeaResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:guid}", UpdateIdea)
            .WithName("UpdateIdea")
            .WithSummary("Update an existing idea")
            .WithDescription("Only the owner of the idea can update it.")
            .Produces<IdeaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete("/{id:guid}", DeleteIdea)
            .WithName("DeleteIdea")
            .WithSummary("Delete an idea")
            .WithDescription("Only the owner of the idea can delete it.")
            .Produces(StatusCodes.Status204NoContent)
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
            return result ? TypedResults.NoContent() : TypedResults.NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }
}