using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Xunit;

namespace CIS_Phase2_Crowdsourced_Ideation.Tests.Features.Ideas;

public class IdeaServiceTests
{
    private readonly AppDbContext _context;
    private readonly IdeaService _service;

    public IdeaServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new IdeaService(_context);
    }

    private ClaimsPrincipal CreateUser(Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task CreateIdea_ReturnsIdeaResponse_WhenValid()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        await _context.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var request = new CreateIdeaRequest(topicId, "New Idea", "Description");

        // Act
        var result = await _service.CreateIdeaAsync(request, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Title, result.Title);
        Assert.Equal(userId, result.OwnerId);
        Assert.False(result.IsWinning);
    }

    [Fact]
    public async Task CreateIdea_ThrowsArgumentException_WhenTopicNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var request = new CreateIdeaRequest("non-existent-topic", "New Idea", "Description");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateIdeaAsync(request, user));
    }

    [Fact]
    public async Task CreateIdea_ThrowsUnauthorized_WhenTopicIsClosed()
    {
        // Arrange
        var topicId = "topic-closed";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Closed Topic", Status = TopicStatus.CLOSED });
        await _context.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var request = new CreateIdeaRequest(topicId, "New Idea", "Description");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateIdeaAsync(request, user));
    }

    [Fact]
    public async Task GetIdeaById_HydratesTitleAndDescription_FromContentJson()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _context.Ideas.Add(new Idea
        {
            Id = id,
            TopicId = topicId,
            OwnerId = ownerId,
            Content = "{\"title\":\"My Idea\",\"description\":\"Some details\",\"isWinning\":false}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Simulate materialization from store (not tracked instance).
        _context.ChangeTracker.Clear();

        // Act
        var result = await _service.GetIdeaByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("My Idea", result!.Title);
        Assert.Equal("Some details", result.Description);
        Assert.False(result.IsWinning);
    }

    [Fact]
    public async Task GetIdeasByTopicId_HydratesTitleAndDescription_FromDoubleEncodedContentJson()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var id = Guid.NewGuid();

        // Stored as a JSON string containing the JSON object (double-encoded).
        _context.Ideas.Add(new Idea
        {
            Id = id,
            TopicId = topicId,
            OwnerId = Guid.NewGuid(),
            Content = "\"{\\\"title\\\":\\\"My Idea\\\",\\\"description\\\":\\\"Some details\\\",\\\"isWinning\\\":true}\"",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = (await _service.GetIdeasByTopicIdAsync(topicId)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("My Idea", result[0].Title);
        Assert.Equal("Some details", result[0].Description);
        Assert.True(result[0].IsWinning);
    }

    [Fact]
    public async Task GetIdeaById_ReturnsIdeaResponse_WhenFound()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var idea = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = Guid.NewGuid(), Title = "Test Idea", Description = "Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Set<Idea>().Add(idea);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetIdeaByIdAsync(idea.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(idea.Id, result.Id);
    }

    [Fact]
    public async Task GetIdeaById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetIdeaByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetIdeasByTopicId_ReturnsIdeas_WhenFound()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var idea1 = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = Guid.NewGuid(), Title = "Test Idea 1", Description = "Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var idea2 = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = Guid.NewGuid(), Title = "Test Idea 2", Description = "Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Set<Idea>().AddRange(idea1, idea2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetIdeasByTopicIdAsync(topicId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetIdeasByTopicId_ReturnsEmpty_WhenNoIdeas()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetIdeasByTopicIdAsync(topicId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateIdea_ReturnsUpdatedIdea_WhenOwner()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var ownerId = Guid.NewGuid();
        var idea = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = ownerId, Title = "Old Title", Description = "Old Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Set<Idea>().Add(idea);
        await _context.SaveChangesAsync();

        var user = CreateUser(ownerId);
        var request = new UpdateIdeaRequest("New Title", "New Description");

        // Act
        var result = await _service.UpdateIdeaAsync(idea.Id, request, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Title, result.Title);
        Assert.Equal(request.Description, result.Description);
        Assert.True(result.UpdatedAt > idea.UpdatedAt);
    }

    [Fact]
    public async Task UpdateIdea_ReturnsNull_WhenIdeaNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);
        var request = new UpdateIdeaRequest("New Title", "New Description");

        // Act
        var result = await _service.UpdateIdeaAsync(Guid.NewGuid(), request, user);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateIdea_ThrowsUnauthorized_WhenNotOwner()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var ownerId = Guid.NewGuid();
        var idea = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = ownerId, Title = "Old Title", Description = "Old Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Set<Idea>().Add(idea);
        await _context.SaveChangesAsync();

        var otherUserId = Guid.NewGuid();
        var otherUser = CreateUser(otherUserId);
        var request = new UpdateIdeaRequest("New Title", "New Description");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateIdeaAsync(idea.Id, request, otherUser));
    }

    [Fact]
    public async Task DeleteIdea_ReturnsTrue_WhenOwner()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var ownerId = Guid.NewGuid();
        var idea = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = ownerId, Title = "Test Idea", Description = "Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Set<Idea>().Add(idea);
        await _context.SaveChangesAsync();

        var user = CreateUser(ownerId);

        // Act
        var result = await _service.DeleteIdeaAsync(idea.Id, user);

        // Assert
        Assert.True(result);
        Assert.Null(await _context.Set<Idea>().FindAsync(idea.Id));
    }

    [Fact]
    public async Task DeleteIdea_ReturnsFalse_WhenIdeaNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUser(userId);

        // Act
        var result = await _service.DeleteIdeaAsync(Guid.NewGuid(), user);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteIdea_ThrowsUnauthorized_WhenNotOwner()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var ownerId = Guid.NewGuid();
        var idea = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = ownerId, Title = "Test Idea", Description = "Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Set<Idea>().Add(idea);
        await _context.SaveChangesAsync();

        var otherUserId = Guid.NewGuid();
        var otherUser = CreateUser(otherUserId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteIdeaAsync(idea.Id, otherUser));
    }

    [Fact]
    public async Task DeleteIdea_DeletesAssociatedVotes()
    {
        // Arrange
        var topicId = "topic-1";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Topic 1" });
        var ownerId = Guid.NewGuid();
        var idea = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = ownerId, Title = "Test Idea", Description = "Desc", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var vote1 = new Vote { Id = Guid.NewGuid(), IdeaId = idea.Id, UserId = Guid.NewGuid() };
        var vote2 = new Vote { Id = Guid.NewGuid(), IdeaId = idea.Id, UserId = Guid.NewGuid() };
        _context.Set<Idea>().Add(idea);
        _context.Set<Vote>().AddRange(vote1, vote2);
        await _context.SaveChangesAsync();

        var user = CreateUser(ownerId);

        // Act
        var result = await _service.DeleteIdeaAsync(idea.Id, user);

        // Assert
        Assert.True(result);
        Assert.Null(await _context.Set<Idea>().FindAsync(idea.Id));
        Assert.Empty(await _context.Set<Vote>().Where(v => v.IdeaId == idea.Id).ToListAsync());
    }

    [Fact]
    public async Task UpdateIdea_ThrowsUnauthorized_WhenTopicIsClosed()
    {
        // Arrange
        var topicId = "topic-closed";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Closed Topic", Status = TopicStatus.CLOSED });
        var ownerId = Guid.NewGuid();
        var idea = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = ownerId,
            Title = "Old",
            Description = "Old",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Ideas.Add(idea);
        await _context.SaveChangesAsync();

        var user = CreateUser(ownerId);
        var request = new UpdateIdeaRequest("New", "New");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.UpdateIdeaAsync(idea.Id, request, user));
    }

    [Fact]
    public async Task DeleteIdea_ThrowsUnauthorized_WhenTopicIsClosed()
    {
        // Arrange
        var topicId = "topic-closed";
        _context.Topics.Add(new Topic { Id = topicId, Title = "Closed Topic", Status = TopicStatus.CLOSED });
        var ownerId = Guid.NewGuid();
        var idea = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = ownerId,
            Title = "Title",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Ideas.Add(idea);
        await _context.SaveChangesAsync();

        var user = CreateUser(ownerId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteIdeaAsync(idea.Id, user));
    }
}
