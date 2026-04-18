using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using Microsoft.AspNetCore.Mvc;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

public static class VoteEndpoints
{
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder endpoints, string version = "v1")
    {
        var group = endpoints.MapGroup($"/{version}/votes")
            .WithTags("Votes");

        // Use version-specific adapter
        group.AddEndpointFilter(async (context, next) =>
        {
            var adapter = version == "v2" 
                ? (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MongoDbAdapter>()
                : (IRepositoryAdapter)context.HttpContext.RequestServices.GetRequiredService<MySqlAdapter>();
            
            context.HttpContext.Items["RepositoryAdapter"] = adapter;
            return await next(context);
        });

        // Public read access (no JWT required)
        group.MapGet("/", HandleGetAllVotes)
            .WithName($"GetAllVotes_{version}")
            .WithSummary($"Get all votes ({version})")
            .Produces<IReadOnlyList<VoteResponse>>(StatusCodes.Status200OK);

        group.MapGet("/idea/{ideaId:guid}", HandleGetVotesByIdea)
            .WithName($"GetVotesByIdea_{version}")
            .WithSummary($"Get votes for an idea ({version})")
            .Produces<IReadOnlyList<VoteResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{voteId:guid}", HandleGetVoteById)
            .WithName($"GetVoteById_{version}")
            .WithSummary($"Get a vote by id ({version})")
            .Produces<VoteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Protected write access (JWT required)
        var protectedGroup = group.MapGroup("/")
            .RequireAuthorization();

        protectedGroup.MapPost("/", HandleCastVote)
            .WithName($"CastVote_{version}")
            .WithSummary($"Cast a vote for an idea ({version})")
            .Produces<VoteResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        protectedGroup.MapPut("/{voteId:guid}", HandleUpdateVote)
            .WithName($"UpdateVote_{version}")
            .WithSummary($"Update a vote ({version})")
            .Produces<VoteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        protectedGroup.MapDelete("/{voteId:guid}", HandleDeleteVote)
            .WithName($"DeleteVote_{version}")
            .WithSummary($"Delete a vote ({version})")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IVoteService GetService(HttpContext http, string version)
    {
        var adapter = (IRepositoryAdapter)http.Items["RepositoryAdapter"]!;
        return new VoteService(adapter, version);
    }

    internal static async Task<IResult> HandleGetAllVotes(HttpContext http, string version)
    {
        var service = GetService(http, version);
        return TypedResults.Ok(await service.GetAllAsync());
    }

    internal static async Task<IResult> HandleGetVotesByIdea(Guid ideaId, HttpContext http, string version)
    {
        var service = GetService(http, version);
        return TypedResults.Ok(await service.GetByIdeaIdAsync(ideaId));
    }

    internal static async Task<Results<Ok<VoteResponse>, NotFound>> HandleGetVoteById(Guid voteId, HttpContext http, string version)
    {
        var service = GetService(http, version);
        var vote = await service.GetByIdAsync(voteId);
        return vote is null ? TypedResults.NotFound() : TypedResults.Ok(vote);
    }

    internal static async Task<IResult> HandleCastVote(CastVoteRequest request, HttpContext http, string version, ClaimsPrincipal user)
    {
        try
        {
            var service = GetService(http, version);
            var created = await service.CastVoteAsync(request, user);
            return TypedResults.Created($"/api/{version}/votes/{created.Id}", created);
        }
        catch (VoteUnauthorizedException)
        {
            return TypedResults.Unauthorized();
        }
        catch (VoteForbiddenException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (VoteNotFoundException ex)
        {
            return TypedResults.NotFound(new { error = ex.Message });
        }
        catch (VoteConflictException ex)
        {
            return TypedResults.Conflict(new ErrorResponse(ex.Message, "DUPLICATE_VOTE"));
        }
    }

    internal static async Task<IResult> HandleUpdateVote(Guid voteId, UpdateVoteRequest request, HttpContext http, string version, ClaimsPrincipal user)
    {
        try
        {
            var service = GetService(http, version);
            var updated = await service.UpdateVoteAsync(voteId, request, user);
            return updated is null ? TypedResults.NotFound() : TypedResults.Ok(updated);
        }
        catch (VoteUnauthorizedException)
        {
            return TypedResults.Unauthorized();
        }
        catch (VoteForbiddenException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (VoteNotFoundException ex)
        {
            return TypedResults.NotFound(new { error = ex.Message });
        }
        catch (VoteConflictException ex)
        {
            return TypedResults.Conflict(new ErrorResponse(ex.Message, "DUPLICATE_VOTE"));
        }
    }

    internal static async Task<IResult> HandleDeleteVote(Guid voteId, HttpContext http, string version, ClaimsPrincipal user)
    {
        try
        {
            var service = GetService(http, version);
            var deleted = await service.DeleteVoteAsync(voteId, user);
            return deleted
                ? TypedResults.Ok(new { message = "Vote deleted.", voteId })
                : TypedResults.NotFound();
        }
        catch (VoteUnauthorizedException)
        {
            return TypedResults.Unauthorized();
        }
        catch (VoteForbiddenException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
