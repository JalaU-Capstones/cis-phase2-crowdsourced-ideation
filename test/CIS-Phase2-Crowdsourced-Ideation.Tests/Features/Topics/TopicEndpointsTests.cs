using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Topics;

public class TopicEndpointsTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // GET /topics
    [Fact]
    public async Task GetAllTopics_ReturnsOk_WithEmptyList()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db);

        var ok = result.Should().BeOfType<Ok<IEnumerable<TopicResponse>>>().Subject;
        ok.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllTopics_ReturnsOk_WithExistingTopics()
    {
        var db = CreateInMemoryDb();
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Topic A",
            Description = "Desc A",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db);

        var ok = result.Should().BeOfType<Ok<IEnumerable<TopicResponse>>>().Subject;
        ok.Value.Should().HaveCount(1);
        ok.Value!.First().Title.Should().Be("Topic A");
    }

    // GET /topics/{id}
    [Fact]
    public async Task GetTopicById_ReturnsOk_WhenTopicExists()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Topic B",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetTopicById(id, db);

        var ok = result.Result.Should().BeOfType<Ok<TopicResponse>>().Subject;
        ok.Value!.Id.Should().Be(id);
        ok.Value.Title.Should().Be("Topic B");
    }

    [Fact]
    public async Task GetTopicById_ReturnsNotFound_WhenTopicDoesNotExist()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetTopicById(Guid.NewGuid().ToString(), db);

        result.Result.Should().BeOfType<NotFound>();
    }

    // POST /topics
    [Fact]
    public async Task CreateTopic_ReturnsCreated_WhenDataIsValid()
    {
        var db     = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        
        var request = new CreateTopicRequest("New Topic", "Some description");
        var user    = TestHelpers.CreateClaimsPrincipal(userId.ToString());
        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        var created = result.Result.Should().BeOfType<Created<TopicResponse>>().Subject;
        created.Value!.Title.Should().Be("New Topic");
        created.Value.Description.Should().Be("Some description");
        created.Value.Status.Should().Be("OPEN");
        created.Value.OwnerId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateTopic_ReturnsBadRequest_WhenTitleIsEmpty()
    {
        var db = CreateInMemoryDb();
        var request = new CreateTopicRequest("", null);
        var user = TestHelpers.CreateClaimsPrincipal(Guid.NewGuid().ToString());

        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task CreateTopic_ReturnsBadRequest_WhenTitleExceedsMaxLength()
    {
        var db = CreateInMemoryDb();
        var request = new CreateTopicRequest(new string('A', 201), null);
        var user = TestHelpers.CreateClaimsPrincipal(Guid.NewGuid().ToString());

        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    // PUT /topics/{id}
    [Fact]
    public async Task UpdateTopic_ReturnsOk_WhenDataIsValidAndUserIsOwner()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Old Title",
            Status = TopicStatus.OPEN,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(ownerId.ToString());
        var request = new UpdateTopicRequest("New Title", "New Desc", "CLOSED");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        var ok = result.Result.Should().BeOfType<Ok<TopicResponse>>().Subject;
        ok.Value!.Title.Should().Be("New Title");
        ok.Value.Description.Should().Be("New Desc");
        ok.Value.Status.Should().Be("CLOSED");
    }

    [Fact]
    public async Task UpdateTopic_ReturnsForbid_WhenUserIsNotOwner()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Old Title",
            Status = TopicStatus.OPEN,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(Guid.NewGuid().ToString());
        var request = new UpdateTopicRequest("New Title", "New Desc", "CLOSED");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsNotFound_WhenTopicDoesNotExist()
    {
        var db = CreateInMemoryDb();
        var user = TestHelpers.CreateClaimsPrincipal(Guid.NewGuid().ToString());
        var request = new UpdateTopicRequest("Title", null, "OPEN");

        var result = await TopicEndpoints.HandleUpdateTopic(Guid.NewGuid().ToString(), request, user, db);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsBadRequest_WhenTitleIsEmpty()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Title",
            Status = TopicStatus.OPEN,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(ownerId.ToString());
        var request = new UpdateTopicRequest("", null, "OPEN");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsBadRequest_WhenStatusIsInvalid()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Title",
            Status = TopicStatus.OPEN,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(ownerId.ToString());
        var request = new UpdateTopicRequest("Title", null, "INVALID");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    // DELETE /topics/{id}
    [Fact]
    public async Task DeleteTopic_ReturnsNoContent_WhenTopicExistsAndUserIsOwner()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Topic to delete",
            Status = TopicStatus.OPEN,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(ownerId.ToString());
        var result = await TopicEndpoints.HandleDeleteTopic(id, user, db);

        result.Result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public async Task DeleteTopic_ReturnsForbid_WhenUserIsNotOwner()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Topic to delete",
            Status = TopicStatus.OPEN,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(Guid.NewGuid().ToString());
        var result = await TopicEndpoints.HandleDeleteTopic(id, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task DeleteTopic_ReturnsNotFound_WhenTopicDoesNotExist()
    {
        var db = CreateInMemoryDb();
        var user = TestHelpers.CreateClaimsPrincipal(Guid.NewGuid().ToString());

        var result = await TopicEndpoints.HandleDeleteTopic(Guid.NewGuid().ToString(), user, db);

        result.Result.Should().BeOfType<NotFound>();
    }
}