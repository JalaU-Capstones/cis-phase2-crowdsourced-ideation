using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Features.Ideas;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Votes;

public static class VoteEndpoints
{
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ideas")
            .WithTags("Votes")
            .RequireAuthorization();
        
        group.MapPost("/{ideaId}/votes", HandleCastVote);
        
        return endpoints;
    }
    
    public static async Task<Results<Ok<VoteResponse>, NotFound<ErrorResponse>, Conflict<ErrorResponse>, UnauthorizedHttpResult>> 
        HandleCastVote(
            string ideaId,
            ClaimsPrincipal user,
            AppDbContext db)
    {
        var login = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (login is null)
            return TypedResults.Unauthorized();
        
        var userRecord = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Login == login);
        
        if (userRecord is null)
            return TypedResults.Unauthorized();
        
        var idea = await db.Set<Idea>().FindAsync(ideaId);
        if (idea is null)
            return TypedResults.NotFound(new ErrorResponse($"Idea with ID '{ideaId}' not found"));
        
        var existingVote = await db.Set<Vote>()
            .AnyAsync(v => v.IdeaId == ideaId && v.UserId == userRecord.Id);
        
        if (existingVote)
            return TypedResults.Conflict(new ErrorResponse(
                $"User '{userRecord.Login}' has already voted on idea '{ideaId}'",
                "DUPLICATE_VOTE"));
        
        var vote = new Vote
        {
            IdeaId = ideaId,
            UserId = userRecord.Id,
            CreatedAt = DateTime.UtcNow
        };
        
        await db.Set<Vote>().AddAsync(vote);
        
        idea.VoteCount++;
        
        await db.SaveChangesAsync();
        
        var response = new VoteResponse(
            vote.Id,
            vote.IdeaId,
            vote.UserId,
            vote.CreatedAt
        );
        
        return TypedResults.Ok(response);
    }
}