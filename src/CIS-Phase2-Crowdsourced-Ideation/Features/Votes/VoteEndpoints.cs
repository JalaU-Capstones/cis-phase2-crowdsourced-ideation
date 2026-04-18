using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

public static class VoteEndpoints
{
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/votes")
            .WithTags("Votes");

        // Public read access (no JWT required)
        group.MapGet("/", HandleGetAllVotes)
            .WithName("GetAllVotes")
            .WithSummary("Get all votes (public)")
            .WithDescription("Public endpoint. Returns votes including idea and topic details.")
            .Produces<IReadOnlyList<VoteResponse>>(StatusCodes.Status200OK);

        group.MapGet("/idea/{ideaId:guid}", HandleGetVotesByIdea)
            .WithName("GetVotesByIdea")
            .WithSummary("Get votes for an idea (public)")
            .WithDescription("Public endpoint. Returns votes for a specific idea, including idea and topic details.")
            .Produces<IReadOnlyList<VoteResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{voteId:guid}", HandleGetVoteById)
            .WithName("GetVoteById")
            .WithSummary("Get a vote by id (public)")
            .WithDescription("Public endpoint. Returns a vote including idea and topic details.")
            .Produces<VoteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Protected write access (JWT required)
        var protectedGroup = group.MapGroup("/")
            .RequireAuthorization();

        protectedGroup.MapPost("/", HandleCastVote)
            .WithName("CastVote")
            .WithSummary("Cast a vote for an idea (authenticated)")
            .WithDescription("""
                Requires authentication.
                Business rules:
                - One vote per user per idea (duplicate votes return 409 Conflict).
                - If the idea's topic is CLOSED, returns 403 Forbidden with: "This topic is closed. Voting is no longer allowed."
                """)
            .Produces<VoteResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        protectedGroup.MapPut("/{voteId:guid}", HandleUpdateVote)
            .WithName("UpdateVote")
            .WithSummary("Update a vote (owner only)")
            .WithDescription("""
                Requires authentication.
                Only the vote owner can modify their vote.
                If the current or target idea belongs to a CLOSED topic, returns 403 Forbidden with: "This topic is closed. Voting is no longer allowed."
                """)
            .Produces<VoteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        protectedGroup.MapDelete("/{voteId:guid}", HandleDeleteVote)
            .WithName("DeleteVote")
            .WithSummary("Delete a vote (owner only)")
            .WithDescription("""
                Requires authentication.
                Only the vote owner can delete their vote.
                If the vote's idea belongs to a CLOSED topic, returns 403 Forbidden with: "This topic is closed. Voting is no longer allowed."
                """)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    internal static async Task<Ok<IReadOnlyList<VoteResponse>>> HandleGetAllVotes(IVoteService service)
        => TypedResults.Ok(await service.GetAllAsync());

    internal static async Task<Ok<IReadOnlyList<VoteResponse>>> HandleGetVotesByIdea(Guid ideaId, IVoteService service)
        => TypedResults.Ok(await service.GetByIdeaIdAsync(ideaId));

    internal static async Task<Results<Ok<VoteResponse>, NotFound>> HandleGetVoteById(Guid voteId, IVoteService service)
    {
        var vote = await service.GetByIdAsync(voteId);
        return vote is null ? TypedResults.NotFound() : TypedResults.Ok(vote);
    }

    internal static async Task<IResult> HandleCastVote(CastVoteRequest request, IVoteService service, ClaimsPrincipal user)
    {
        try
        {
            var created = await service.CastVoteAsync(request, user);
            return TypedResults.Created($"/api/v1/votes/{created.Id}", created);
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

    internal static async Task<IResult> HandleUpdateVote(Guid voteId, UpdateVoteRequest request, IVoteService service, ClaimsPrincipal user)
    {
        try
        {
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

    internal static async Task<IResult> HandleDeleteVote(Guid voteId, IVoteService service, ClaimsPrincipal user)
    {
        try
        {
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
