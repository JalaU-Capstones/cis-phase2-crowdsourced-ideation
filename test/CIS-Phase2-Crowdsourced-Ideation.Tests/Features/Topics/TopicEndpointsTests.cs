using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

    private static async Task<UserRecord> SeedUser(AppDbContext db, string login)
    {
        var user = new UserRecord
        {
            Id = Guid.NewGuid().ToString(),
            Login = login,
            Name = login,
            Password = "password"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // GET /topics
   [Fact]
    public async Task GetAllTopics_ReturnsOk_WithEmptyList()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.Should().BeEmpty();
        ok.Value.TotalItems.Should().Be(0);
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
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.TotalItems.Should().Be(1);
        ok.Value.Data.First().Title.Should().Be("Topic A");
    }

    [Fact]
    public async Task GetAllTopics_IncludesWinningIdea_WhenTopicIsClosed()
    {
        var db = CreateInMemoryDb();
        var topicId = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = topicId,
            Title = "Closed Topic",
            Description = "Desc",
            Status = TopicStatus.CLOSED,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Ideas.Add(new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.NewGuid(),
            Title = "Winner",
            Description = "Winning idea",
            IsWinning = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.Should().HaveCount(1);
        ok.Value!.Data.First().WinningIdea.Should().NotBeNull();
        ok.Value!.Data.First().WinningIdea!.Title.Should().Be("Winner");
    }

    [Fact]
    public async Task GetAllTopics_DoesNotIncludeWinningIdea_WhenTopicIsOpen()
    {
        var db = CreateInMemoryDb();
        var topicId = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = topicId,
            Title = "Open Topic",
            Description = "Desc",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Even if an idea is marked as winning, OPEN topics must not surface it.
        db.Ideas.Add(new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.NewGuid(),
            Title = "Winner",
            Description = "Winning idea",
            IsWinning = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.Should().HaveCount(1);
        ok.Value!.Data.First().WinningIdea.Should().BeNull();
    }

    [Fact]
    public async Task GetAllTopics_ClosedTopicWithoutWinner_ReturnsNullWinningIdea()
    {
        var db = CreateInMemoryDb();
        var topicId = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = topicId,
            Title = "Closed Topic",
            Status = TopicStatus.CLOSED,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Ideas.Add(new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.NewGuid(),
            Title = "Not winner",
            Description = "No",
            IsWinning = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.Should().HaveCount(1);
        ok.Value!.Data.First().WinningIdea.Should().BeNull();
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
            OwnerId = Guid.NewGuid().ToString(),
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
    public async Task GetTopicById_IncludesWinningIdea_WhenTopicIsClosed()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Closed Topic",
            Status = TopicStatus.CLOSED,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Ideas.Add(new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = id,
            OwnerId = Guid.NewGuid(),
            Title = "Winner",
            Description = "Winning idea",
            IsWinning = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetTopicById(id, db);

        var ok = result.Result.Should().BeOfType<Ok<TopicResponse>>().Subject;
        ok.Value!.WinningIdea.Should().NotBeNull();
        ok.Value.WinningIdea!.Title.Should().Be("Winner");
    }

    [Fact]
    public async Task GetTopicById_DoesNotIncludeWinningIdea_WhenTopicIsOpen()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Open Topic",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Ideas.Add(new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = id,
            OwnerId = Guid.NewGuid(),
            Title = "Winner",
            Description = "Winning idea",
            IsWinning = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetTopicById(id, db);
        var ok = result.Result.Should().BeOfType<Ok<TopicResponse>>().Subject;
        ok.Value!.WinningIdea.Should().BeNull();
    }

    [Fact]
    public async Task GetTopicById_ClosedTopicWithoutWinner_ReturnsNullWinningIdea()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Closed Topic",
            Status = TopicStatus.CLOSED,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Ideas.Add(new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = id,
            OwnerId = Guid.NewGuid(),
            Title = "Not winner",
            Description = "No",
            IsWinning = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetTopicById(id, db);
        var ok = result.Result.Should().BeOfType<Ok<TopicResponse>>().Subject;
        ok.Value!.WinningIdea.Should().BeNull();
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
        var login = "testuser";
        var dbUser = await SeedUser(db, login);
        
        var request = new CreateTopicRequest("New Topic", "Some description");
        var user    = TestHelpers.CreateClaimsPrincipal(login);
        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        var created = result.Result.Should().BeOfType<Created<TopicResponse>>().Subject;
        created.Value!.Title.Should().Be("New Topic");
        created.Value.Description.Should().Be("Some description");
        created.Value.Status.Should().Be("OPEN");
        created.Value.OwnerId.Should().Be(dbUser.Id);
    }

    [Fact]
    public async Task CreateTopic_ReturnsUnauthorized_WhenUserIdentityMissing()
    {
        var db = CreateInMemoryDb();
        var request = new CreateTopicRequest("New Topic", "Some description");
        var user = new ClaimsPrincipal(new ClaimsIdentity()); // no sub/NameIdentifier

        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        result.Result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task CreateTopic_ReturnsUnauthorized_WhenUserNotFoundInDatabase()
    {
        var db = CreateInMemoryDb();
        var request = new CreateTopicRequest("New Topic", "Some description");
        var user = TestHelpers.CreateClaimsPrincipal("missing-user");

        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        result.Result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task CreateTopic_ReturnsBadRequest_WhenTitleIsEmpty()
    {
        var db = CreateInMemoryDb();
        var login = "testuser";
        await SeedUser(db, login); // Seed user for authentication check
        var request = new CreateTopicRequest("", null);
        var user = TestHelpers.CreateClaimsPrincipal(login);

        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task CreateTopic_ReturnsBadRequest_WhenTitleExceedsMaxLength()
    {
        var db = CreateInMemoryDb();
        var login = "testuser";
        await SeedUser(db, login); // Seed user for authentication check
        var request = new CreateTopicRequest(new string('A', 201), null);
        var user = TestHelpers.CreateClaimsPrincipal(login);

        var result = await TopicEndpoints.HandleCreateTopic(request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    // PUT /topics/{id}
    [Fact]
    public async Task UpdateTopic_ReturnsOk_WhenDataIsValidAndUserIsOwner()
    {
        var db = CreateInMemoryDb();
        var login = "owner";
        var dbUser = await SeedUser(db, login);
        var id = Guid.NewGuid().ToString();
        
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Old Title",
            Status = TopicStatus.OPEN,
            OwnerId = dbUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(login);
        var request = new UpdateTopicRequest("New Title", "New Desc", "CLOSED");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        var ok = result.Result.Should().BeOfType<Ok<TopicResponse>>().Subject;
        ok.Value!.Title.Should().Be("New Title");
        ok.Value.Description.Should().Be("New Desc");
        ok.Value.Status.Should().Be("CLOSED");
    }

    [Fact]
    public async Task UpdateTopic_ReturnsForbid_WhenUserIdentityMissing()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Old Title",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity()); // no sub/NameIdentifier
        var request = new UpdateTopicRequest("New Title", "New Desc", "OPEN");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsForbid_WhenUserNotFoundInDatabase()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Old Title",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal("missing-user");
        var request = new UpdateTopicRequest("New Title", "New Desc", "OPEN");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsForbid_WhenUserIsNotOwner()
    {
        var db = CreateInMemoryDb();
        var ownerLogin = "owner";
        var otherLogin = "other";
        var owner = await SeedUser(db, ownerLogin);
        await SeedUser(db, otherLogin);

        var id = Guid.NewGuid().ToString();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Old Title",
            Status = TopicStatus.OPEN,
            OwnerId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(otherLogin);
        var request = new UpdateTopicRequest("New Title", "New Desc", "CLOSED");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsNotFound_WhenTopicDoesNotExist()
    {
        var db = CreateInMemoryDb();
        var login = "testuser";
        await SeedUser(db, login);
        var user = TestHelpers.CreateClaimsPrincipal(login);
        var request = new UpdateTopicRequest("Title", null, "OPEN");

        var result = await TopicEndpoints.HandleUpdateTopic(Guid.NewGuid().ToString(), request, user, db);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsBadRequest_WhenTitleIsEmpty()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var login = "owner";
        var dbUser = await SeedUser(db, login);
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Title",
            Status = TopicStatus.OPEN,
            OwnerId = dbUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(login);
        var request = new UpdateTopicRequest("", null, "OPEN");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsBadRequest_WhenStatusIsInvalid()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var login = "owner";
        var dbUser = await SeedUser(db, login);
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Title",
            Status = TopicStatus.OPEN,
            OwnerId = dbUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(login);
        var request = new UpdateTopicRequest("Title", null, "INVALID");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task UpdateTopic_ReturnsBadRequest_WhenTryingToReopenClosedTopic()
    {
        var db = CreateInMemoryDb();
        var id = Guid.NewGuid().ToString();
        var login = "owner";
        var dbUser = await SeedUser(db, login);

        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Title",
            Status = TopicStatus.CLOSED,
            OwnerId = dbUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(login);
        var request = new UpdateTopicRequest("New Title", "New Desc", "OPEN");
        var result = await TopicEndpoints.HandleUpdateTopic(id, request, user, db);

        result.Result.Should().BeOfType<BadRequest<object>>();
    }

    // DELETE /topics/{id}
    [Fact]
    public async Task DeleteTopic_ReturnsOk_WithMessage_WhenTopicExistsAndUserIsOwner()
    {
        var db = CreateInMemoryDb();
        var login = "owner";
        var dbUser = await SeedUser(db, login);
        var id = Guid.NewGuid().ToString();
        
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Topic to delete",
            Status = TopicStatus.OPEN,
            OwnerId = dbUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(login);
        var result = await TopicEndpoints.HandleDeleteTopic(id, user, db);

        var ok = result.Result.Should().BeOfType<Ok<object>>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value!.ToString().Should().Contain("deleted all related ideas and votes");
    }

    [Fact]
    public async Task DeleteTopic_DeletesAssociatedIdeasAndVotes()
    {
        var db = CreateInMemoryDb();
        var login = "owner";
        var dbUser = await SeedUser(db, login);
        var topicId = Guid.NewGuid().ToString();

        db.Topics.Add(new Topic
        {
            Id = topicId,
            Title = "Topic to delete",
            Status = TopicStatus.OPEN,
            OwnerId = dbUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var idea1 = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.NewGuid(),
            Title = "Idea 1",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var idea2 = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.NewGuid(),
            Title = "Idea 2",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Ideas.AddRange(idea1, idea2);
        db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = idea1.Id, UserId = Guid.NewGuid() });
        db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = idea2.Id, UserId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(login);
        var result = await TopicEndpoints.HandleDeleteTopic(topicId, user, db);

        result.Result.Should().BeOfType<Ok<object>>();
        (await db.Topics.FindAsync(topicId)).Should().BeNull();
        (await db.Ideas.Where(i => i.TopicId == topicId).ToListAsync()).Should().BeEmpty();
        (await db.Votes.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTopic_ReturnsForbid_WhenUserIdentityMissing()
    {
        var db = CreateInMemoryDb();
        var topicId = Guid.NewGuid().ToString();
        db.Topics.Add(new Topic
        {
            Id = topicId,
            Title = "Topic",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var result = await TopicEndpoints.HandleDeleteTopic(topicId, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task DeleteTopic_ReturnsForbid_WhenUserNotFoundInDatabase()
    {
        var db = CreateInMemoryDb();
        var topicId = Guid.NewGuid().ToString();
        db.Topics.Add(new Topic
        {
            Id = topicId,
            Title = "Topic",
            Status = TopicStatus.OPEN,
            OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal("missing-user");
        var result = await TopicEndpoints.HandleDeleteTopic(topicId, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task DeleteTopic_ReturnsForbid_WhenUserIsNotOwner()
    {
        var db = CreateInMemoryDb();
        var ownerLogin = "owner";
        var otherLogin = "other";
        var owner = await SeedUser(db, ownerLogin);
        await SeedUser(db, otherLogin);

        var id = Guid.NewGuid().ToString();
        db.Topics.Add(new Topic
        {
            Id = id,
            Title = "Topic to delete",
            Status = TopicStatus.OPEN,
            OwnerId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var user = TestHelpers.CreateClaimsPrincipal(otherLogin);
        var result = await TopicEndpoints.HandleDeleteTopic(id, user, db);

        result.Result.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task DeleteTopic_ReturnsNotFound_WhenTopicDoesNotExist()
    {
        var db = CreateInMemoryDb();
        var login = "testuser";
        await SeedUser(db, login);
        var user = TestHelpers.CreateClaimsPrincipal(login);

        var result = await TopicEndpoints.HandleDeleteTopic(Guid.NewGuid().ToString(), user, db);

        result.Result.Should().BeOfType<NotFound>();
    }
    // --- PAGINATION ---

    [Fact]
    public async Task GetAllTopics_ReturnsPaginatedResponse_WithDefaultValues()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.CurrentPage.Should().Be(0);
        ok.Value.PageSize.Should().Be(10);
        ok.Value.TotalItems.Should().Be(0);
        ok.Value.TotalPages.Should().Be(0);
        ok.Value.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllTopics_ReturnsPaginatedResponse_WithCustomPageAndSize()
    {
        var db = CreateInMemoryDb();
        for (int i = 0; i < 15; i++)
            db.Topics.Add(new Topic
            {
                Id = Guid.NewGuid().ToString(), Title = $"Topic {i}",
                Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, page: 1, size: 5, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.CurrentPage.Should().Be(1);
        ok.Value.PageSize.Should().Be(5);
        ok.Value.TotalItems.Should().Be(15);
        ok.Value.TotalPages.Should().Be(3);
        ok.Value.Data.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllTopics_ReturnsBadRequest_WhenPageIsNegative()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, page: -1, size: 10, null, null, null, null);

        result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task GetAllTopics_ReturnsBadRequest_WhenSizeIsZero()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, page: 0, size: 0, null, null, null, null);

        result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task GetAllTopics_ReturnsBadRequest_WhenSizeIsNegative()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, page: 0, size: -5, null, null, null, null);

        result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task GetAllTopics_ReturnsLastPage_WhenPageExceedsTotalPages()
    {
        var db = CreateInMemoryDb();
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Only Topic",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, page: 99, size: 10, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.Should().BeEmpty();
        ok.Value.TotalItems.Should().Be(1);
    }

    // --- FILTERING ---

    [Fact]
    public async Task GetAllTopics_FiltersByStatus_ReturnsOnlyMatchingTopics()
    {
        var db = CreateInMemoryDb();
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Open Topic",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Closed Topic",
            Status = TopicStatus.CLOSED, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, status: "OPEN", null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.TotalItems.Should().Be(1);
        ok.Value.Data.Should().OnlyContain(t => t.Status == "OPEN");
    }

    [Fact]
    public async Task GetAllTopics_FiltersByOwnerId_ReturnsOnlyMatchingTopics()
    {
        var db = CreateInMemoryDb();
        var ownerId = Guid.NewGuid().ToString();
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Owner Topic",
            Status = TopicStatus.OPEN, OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Other Topic",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, ownerId: ownerId, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.TotalItems.Should().Be(1);
        ok.Value.Data.Should().OnlyContain(t => t.OwnerId == ownerId);
    }

    [Fact]
    public async Task GetAllTopics_ReturnsEmptyList_WhenFilterMatchesNoRecords()
    {
        var db = CreateInMemoryDb();
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Open Topic",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, status: "CLOSED", null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.TotalItems.Should().Be(0);
        ok.Value.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllTopics_ReturnsBadRequest_WhenStatusIsInvalid()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, status: "INVALID", null, null, null);

        result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task GetAllTopics_FiltersByStatusAndOwnerId_WhenBothProvided()
    {
        var db = CreateInMemoryDb();
        var ownerId = Guid.NewGuid().ToString();
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Match",
            Status = TopicStatus.OPEN, OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Wrong Status",
            Status = TopicStatus.CLOSED, OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Wrong Owner",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, status: "OPEN", ownerId: ownerId, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.TotalItems.Should().Be(1);
        ok.Value.Data.First().Title.Should().Be("Match");
    }

    // --- SORTING ---

    [Fact]
    public async Task GetAllTopics_SortsByCreatedAtDesc_ByDefault()
    {
        var db = CreateInMemoryDb();
        var baseTime = DateTime.UtcNow;
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Older",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = baseTime.AddHours(-2), UpdatedAt = baseTime.AddHours(-2)
        });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Newer",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = baseTime, UpdatedAt = baseTime
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, null);

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.First().Title.Should().Be("Newer");
    }

    [Fact]
    public async Task GetAllTopics_SortsByTitleAsc_WhenSortByTitleAndOrderAsc()
    {
        var db = CreateInMemoryDb();
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Zebra",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Apple",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, sortBy: "title", order: "asc");

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.First().Title.Should().Be("Apple");
    }

    [Fact]
    public async Task GetAllTopics_SortsByUpdatedAtAsc_WhenRequested()
    {
        var db = CreateInMemoryDb();
        var baseTime = DateTime.UtcNow;
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Updated Recently",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = baseTime, UpdatedAt = baseTime
        });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Updated Long Ago",
            Status = TopicStatus.OPEN, OwnerId = Guid.NewGuid().ToString(),
            CreatedAt = baseTime, UpdatedAt = baseTime.AddHours(-5)
        });
        await db.SaveChangesAsync();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, sortBy: "updatedAt", order: "asc");

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.Data.First().Title.Should().Be("Updated Long Ago");
    }

    [Fact]
    public async Task GetAllTopics_ReturnsBadRequest_WhenSortByIsInvalid()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, sortBy: "invalid", null);

        result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task GetAllTopics_ReturnsBadRequest_WhenOrderIsInvalid()
    {
        var db = CreateInMemoryDb();

        var result = await TopicEndpoints.HandleGetAllTopics(db, null, null, null, null, null, order: "invalid");

        result.Should().BeOfType<BadRequest<object>>();
    }

    // --- COMBINED ---

    [Fact]
    public async Task GetAllTopics_AppliesFilterThenSortThenPagination_WhenAllParamsProvided()
    {
        var db = CreateInMemoryDb();
        var ownerId = Guid.NewGuid().ToString();
        var baseTime = DateTime.UtcNow;

        // 3 OPEN topics from our owner, 1 CLOSED (should be filtered out)
        for (int i = 0; i < 3; i++)
            db.Topics.Add(new Topic
            {
                Id = Guid.NewGuid().ToString(), Title = $"Topic {(char)('C' - i)}", // C, B, A
                Status = TopicStatus.OPEN, OwnerId = ownerId,
                CreatedAt = baseTime.AddMinutes(i), UpdatedAt = baseTime.AddMinutes(i)
            });
        db.Topics.Add(new Topic
        {
            Id = Guid.NewGuid().ToString(), Title = "Closed Topic",
            Status = TopicStatus.CLOSED, OwnerId = ownerId,
            CreatedAt = baseTime, UpdatedAt = baseTime
        });
        await db.SaveChangesAsync();

        // Filter: OPEN + ownerId | Sort: title asc | Page: 0, size: 2
        var result = await TopicEndpoints.HandleGetAllTopics(
            db, page: 0, size: 2, status: "OPEN", ownerId: ownerId, sortBy: "title", order: "asc");

        var ok = result.Should().BeOfType<Ok<PagedResponse<TopicResponse>>>().Subject;
        ok.Value!.TotalItems.Should().Be(3);   // 3 OPEN after filter
        ok.Value.TotalPages.Should().Be(2);    // ceil(3/2)
        ok.Value.Data.Should().HaveCount(2);   // page 0 with size 2
        ok.Value.Data.First().Title.Should().Be("Topic A"); // sorted asc
    }
}

