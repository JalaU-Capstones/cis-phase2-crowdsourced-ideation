using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Features;
using Microsoft.AspNetCore.Mvc;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public static class IdeaEndpoints
{
    public static void MapIdeaEndpoints(this IEndpointRouteBuilder app, string version = "v1")
    {
        var group = app.MapGroup($"/{version}/ideas")
            .WithTags("Ideas");

        // Use version-specific adapter
        group.AddEndpointFilter(async (context, next) =>
        {
            var adapter = version == "v2" 
                ? (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MongoDbAdapter>()
                : (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MySqlAdapter>();
            
            context.HttpContext.Items["RepositoryAdapter"] = adapter;
            return await next(context);
        });

        group.MapGet("/", GetAllIdeas)
            .WithName($"GetAllIdeas_{version}")
            .WithSummary($"Get all ideas ({version})")
            .Produces<PagedResponse<IdeaResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetIdea)
            .WithName($"GetIdea_{version}")
            .WithSummary($"Get an idea by its ID ({version})")
            .Produces<IdeaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/topic/{topicId}", GetIdeasByTopic)
            .WithName($"GetIdeasByTopic_{version}")
            .WithSummary($"Get all ideas for a specific topic ({version})")
            .Produces<IEnumerable<IdeaResponse>>(StatusCodes.Status200OK);

        // Protected write access
        var protectedGroup = group.MapGroup("/")
            .RequireAuthorization();

        protectedGroup.MapPost("/", CreateIdea)
            .WithName($"CreateIdea_{version}")
            .WithSummary($"Create a new idea ({version})")
            .Produces<IdeaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapPut("/{id:guid}", UpdateIdea)
            .WithName($"UpdateIdea_{version}")
            .WithSummary($"Update an existing idea ({version})")
            .Produces<IdeaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapDelete("/{id:guid}", DeleteIdea)
            .WithName($"DeleteIdea_{version}")
            .WithSummary($"Delete an idea ({version})")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static IIdeaService GetService(HttpContext http, string version)
    {
        var adapter = (IRepositoryAdapter)http.Items["RepositoryAdapter"]!;
        return new IdeaService(adapter, version);
    }

    private static async Task<IResult> CreateIdea(
        CreateIdeaRequest request, HttpContext http, string version, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.TopicId) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return TypedResults.BadRequest("Required fields are missing");
        }

        try 
        {
            var service = GetService(http, version);
            var result = await service.CreateIdeaAsync(request, user);
            return TypedResults.Created($"/api/{version}/ideas/{result.Id}", result);
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
        HttpContext http,
        string version,
        [FromQuery] int? page,
        [FromQuery] int? size,
        [FromQuery] string? sortBy,
        [FromQuery] string? order)
    {
        try
        {
            var service = GetService(http, version);
            var result = await service.GetAllIdeasAsync(page, size, sortBy, order);
            return TypedResults.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetIdea(Guid id, HttpContext http, string version)
    {
        var service = GetService(http, version);
        var result = await service.GetIdeaByIdAsync(id);
        return result == null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    private static async Task<IResult> GetIdeasByTopic(string topicId, HttpContext http, string version)
    {
        var service = GetService(http, version);
        var result = await service.GetIdeasByTopicIdAsync(topicId);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> UpdateIdea(
        Guid id, UpdateIdeaRequest request, HttpContext http, string version, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return TypedResults.BadRequest("Required fields are missing");
        }

        try
        {
            var service = GetService(http, version);
            var result = await service.UpdateIdeaAsync(id, request, user);
            return result == null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> DeleteIdea(Guid id, HttpContext http, string version, ClaimsPrincipal user)
    {
        try
        {
            var service = GetService(http, version);
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
