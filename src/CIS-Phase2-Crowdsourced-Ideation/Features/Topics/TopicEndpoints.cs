using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

public static class TopicEndpoints
{
    public static IEndpointRouteBuilder MapTopicEndpoints(this IEndpointRouteBuilder endpoints, string version = "v1")
    {
        var group = endpoints.MapGroup($"/api/{version}/topics")
            .WithTags("Topics");

        group.AddEndpointFilter(async (context, next) =>
        {
            var adapter = version == "v2" 
                ? (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MongoDbAdapter>()
                : (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MySqlAdapter>();
            
            context.HttpContext.Items["RepositoryAdapter"] = adapter;
            return await next(context);
        });

        group.MapGet("/", (HttpContext http, int? page, int? size, string? status, string? ownerId, string? sortBy, string? order) =>
                HandleGetAllTopics(http, page, size, status, ownerId, sortBy, order, version))
            .WithName($"GetAllTopics_{version}")
            .WithSummary($"Get all topics ({version})")
            .Produces<PagedResponse<TopicResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", (string id, HttpContext http) =>
                HandleGetTopicById(id, http, version))
            .WithName($"GetTopicById_{version}")
            .WithSummary($"Get topic by id ({version})")
            .Produces<TopicResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        var protectedGroup = group.MapGroup("/")
            .RequireAuthorization();

        protectedGroup.MapPost("/", (CreateTopicRequest request, ClaimsPrincipal user, HttpContext http) =>
                HandleCreateTopic(request, user, http, version))
            .WithName($"CreateTopic_{version}")
            .WithSummary($"Create a topic ({version})")
            .Produces<TopicResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapPut("/{id}", (string id, UpdateTopicRequest request, ClaimsPrincipal user, HttpContext http) =>
                HandleUpdateTopic(id, request, user, http, version))
            .WithName($"UpdateTopic_{version}")
            .WithSummary($"Update a topic ({version})")
            .Produces<TopicResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        protectedGroup.MapDelete("/{id}", (string id, ClaimsPrincipal user, HttpContext http) =>
                HandleDeleteTopic(id, user, http, version))
            .WithName($"DeleteTopic_{version}")
            .WithSummary($"Delete a topic ({version})")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static ITopicService GetService(HttpContext http, string version)
    {
        var adapter = (IRepositoryAdapter)http.Items["RepositoryAdapter"]!;
        return new TopicService(adapter, version);
    }

    public static async Task<IResult> HandleGetAllTopics(
        HttpContext http,
        [FromQuery] int? page, [FromQuery] int? size,
        [FromQuery] string? status, [FromQuery] string? ownerId,
        [FromQuery] string? sortBy, [FromQuery] string? order,
        string version)
    {
        try
        {
            var service = GetService(http, version);
            var response = await service.GetAllTopicsAsync(page, size, status, ownerId, sortBy, order);
            return TypedResults.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }
    }

    public static async Task<Results<Ok<TopicResponse>, NotFound>> HandleGetTopicById(
        string id, HttpContext http, string version)
    {
        var service = GetService(http, version);
        var topic = await service.GetTopicByIdAsync(id);
        if (topic is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(topic);
    }

    public static async Task<Results<Created<TopicResponse>, BadRequest<object>, UnauthorizedHttpResult>> HandleCreateTopic(
        CreateTopicRequest request,
        ClaimsPrincipal user,
        HttpContext http,
        string version)
    {
        try
        {
            var service = GetService(http, version);
            var topic = await service.CreateTopicAsync(request, user);
            return TypedResults.Created($"/api/{version}/topics/{topic.Id}", topic);
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

    public static async Task<Results<Ok<TopicResponse>, NotFound, BadRequest<object>, ForbidHttpResult>> HandleUpdateTopic(
        string id,
        UpdateTopicRequest request,
        ClaimsPrincipal user,
        HttpContext http,
        string version)
    {
        try
        {
            var service = GetService(http, version);
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

    public static async Task<Results<Ok<object>, NotFound, ForbidHttpResult>> HandleDeleteTopic(
        string id,
        ClaimsPrincipal user,
        HttpContext http,
        string version)
    {
        try
        {
            var service = GetService(http, version);
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
}
