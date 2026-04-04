using CIS.Phase2.CrowdsourcedIdeation.Features.Ideas;
using CIS.Phase2.CrowdsourcedIdeation.Features.Votes;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Votes;

public class VoteEndpointsTests
{
    private async Task<AppDbContext> GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }
    
    private ClaimsPrincipal CreateUserPrincipal(string login, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, login),
            new Claim("sub", login)
        };
        
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }
    
    [Fact]
    public async Task HandleCastVote_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var db = await GetDbContext();
        var userId = Guid.NewGuid().ToString();
        var userLogin = "testuser";
        
        var user = new UserRecord
        {
            Id = userId,
            Name = "Test User",
            Login = userLogin,
            Password = "hashed"
        };
        await db.Users.AddAsync(user);
        
        var idea = new Idea
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Idea",
            Description = "Description",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await db.Set<Idea>().AddAsync(idea);
        await db.SaveChangesAsync();
        
        var principal = CreateUserPrincipal(userLogin, userId);
        
        // Act
        var result = await VoteEndpoints.HandleCastVote(idea.Id, principal, db);
        
        // Assert
        var okResult = Assert.IsType<Ok<VoteResponse>>(result);
        Assert.Equal(idea.Id, okResult.Value.IdeaId);
        Assert.Equal(userId, okResult.Value.UserId);
        
        var voteExists = await db.Set<Vote>().AnyAsync(v => v.IdeaId == idea.Id && v.UserId == userId);
        Assert.True(voteExists);
        
        var updatedIdea = await db.Set<Idea>().FindAsync(idea.Id);
        Assert.Equal(1, updatedIdea!.VoteCount);
    }
    
    [Fact]
    public async Task HandleCastVote_WithNonExistentIdea_ReturnsNotFound()
    {
        // Arrange
        var db = await GetDbContext();
        var userId = Guid.NewGuid().ToString();
        var userLogin = "testuser";
        
        var user = new UserRecord
        {
            Id = userId,
            Name = "Test User",
            Login = userLogin,
            Password = "hashed"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        
        var principal = CreateUserPrincipal(userLogin, userId);
        var nonExistentId = Guid.NewGuid().ToString();
        
        // Act
        var result = await VoteEndpoints.HandleCastVote(nonExistentId, principal, db);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFound<ErrorResponse>>(result);
        Assert.Contains("not found", notFoundResult.Value!.Message);
    }
    
    [Fact]
    public async Task HandleCastVote_WithDuplicateVote_ReturnsConflict()
    {
        // Arrange
        var db = await GetDbContext();
        var userId = Guid.NewGuid().ToString();
        var userLogin = "testuser";
        
        var user = new UserRecord
        {
            Id = userId,
            Name = "Test User",
            Login = userLogin,
            Password = "hashed"
        };
        await db.Users.AddAsync(user);
        
        var idea = new Idea
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Test Idea",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await db.Set<Idea>().AddAsync(idea);
        
        var existingVote = new Vote
        {
            IdeaId = idea.Id,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        await db.Set<Vote>().AddAsync(existingVote);
        await db.SaveChangesAsync();
        
        var principal = CreateUserPrincipal(userLogin, userId);
        
        // Act
        var result = await VoteEndpoints.HandleCastVote(idea.Id, principal, db);
        
        // Assert
        var conflictResult = Assert.IsType<Conflict<ErrorResponse>>(result);
        Assert.Equal("DUPLICATE_VOTE", conflictResult.Value!.ErrorCode);
    }
    
    [Fact]
    public async Task HandleCastVote_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var db = await GetDbContext();
        var principal = new ClaimsPrincipal();
        
        // Act
        var result = await VoteEndpoints.HandleCastVote("any-id", principal, db);
        
        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}