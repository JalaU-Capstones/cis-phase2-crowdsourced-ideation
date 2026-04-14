using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Votes;

public sealed class VoteApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Phase1SecretKeyHex =
        "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName = $"VotesTestDb-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _dbRoot = new();

    public VoteApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbDescriptor is not null)
                    services.Remove(dbDescriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName, _dbRoot));

                var signingKeyBytes = Enumerable.Range(0, Phase1SecretKeyHex.Length / 2)
                    .Select(x => Convert.ToByte(Phase1SecretKeyHex.Substring(x * 2, 2), 16))
                    .ToArray();

                var signingKey = new SymmetricSecurityKey(signingKeyBytes);

                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = signingKey,
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
            });
        });
    }

    private static string TokenForLogin(string login) =>
        TestHelpers.GenerateJwtToken(Phase1SecretKeyHex, username: login);

    private async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> act)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await act(db);
    }

    [Fact]
    public async Task GetVotesEndpoints_ArePublic()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/votes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/votes/idea/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/votes/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostVote_RequiresAuthentication()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/votes", new CastVoteRequest(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CastVote_HappyPath_ReturnsCreated_AndVoteIsQueryable()
    {
        var login = "voter1";
        var voter = new UserRecord { Id = Guid.NewGuid().ToString(), Login = login, Name = login, Password = "pw" };
        var owner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "owner", Name = "owner", Password = "pw" };
        var topicId = Guid.NewGuid().ToString();
        var ideaId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Users.AddRange(voter, owner);
            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Topic A",
                Status = TopicStatus.OPEN,
                OwnerId = owner.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Ideas.Add(new Idea
            {
                Id = ideaId,
                TopicId = topicId,
                OwnerId = Guid.Parse(owner.Id),
                Title = "Idea 1",
                Description = "Desc",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenForLogin(login));

        var post = await client.PostAsJsonAsync("/api/votes", new CastVoteRequest(ideaId));
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await post.Content.ReadFromJsonAsync<VoteResponse>();
        created.Should().NotBeNull();
        created!.IdeaId.Should().Be(ideaId);
        created.IdeaTitle.Should().Be("Idea 1");
        created.TopicId.Should().Be(topicId);
        created.TopicTitle.Should().Be("Topic A");

        var allVotes = await client.GetFromJsonAsync<List<VoteResponse>>("/api/votes");
        allVotes.Should().NotBeNull();
        allVotes!.Should().ContainSingle(v => v.Id == created.Id);

        var byIdea = await client.GetFromJsonAsync<List<VoteResponse>>($"/api/votes/idea/{ideaId}");
        byIdea!.Should().ContainSingle(v => v.Id == created.Id);

        var byId = await client.GetFromJsonAsync<VoteResponse>($"/api/votes/{created.Id}");
        byId.Should().NotBeNull();
        byId!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task CastVote_DuplicateVote_Returns409()
    {
        var login = "voter1";
        var userId = Guid.NewGuid();
        var voter = new UserRecord { Id = userId.ToString(), Login = login, Name = login, Password = "pw" };
        var owner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "owner", Name = "owner", Password = "pw" };
        var topicId = Guid.NewGuid().ToString();
        var ideaId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Users.AddRange(voter, owner);
            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Topic A",
                Status = TopicStatus.OPEN,
                OwnerId = owner.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Ideas.Add(new Idea
            {
                Id = ideaId,
                TopicId = topicId,
                OwnerId = Guid.Parse(owner.Id),
                Title = "Idea 1",
                Description = "Desc",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenForLogin(login));

        (await client.PostAsJsonAsync("/api/votes", new CastVoteRequest(ideaId))).StatusCode.Should().Be(HttpStatusCode.Created);
        var dup = await client.PostAsJsonAsync("/api/votes", new CastVoteRequest(ideaId));

        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await dup.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.ErrorCode.Should().Be("DUPLICATE_VOTE");
    }

    [Fact]
    public async Task ClosedTopic_PreventsCastingAndDeletingVotes()
    {
        var login = "voter1";
        var voter = new UserRecord { Id = Guid.NewGuid().ToString(), Login = login, Name = login, Password = "pw" };
        var owner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "owner", Name = "owner", Password = "pw" };
        var topicId = Guid.NewGuid().ToString();
        var ideaId = Guid.NewGuid();
        var voteId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Users.AddRange(voter, owner);
            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Closed Topic",
                Status = TopicStatus.CLOSED,
                OwnerId = owner.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Ideas.Add(new Idea
            {
                Id = ideaId,
                TopicId = topicId,
                OwnerId = Guid.Parse(owner.Id),
                Title = "Idea 1",
                Description = "Desc",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Votes.Add(new Vote { Id = voteId, IdeaId = ideaId, UserId = Guid.Parse(voter.Id) });
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenForLogin(login));

        var cast = await client.PostAsJsonAsync("/api/votes", new CastVoteRequest(ideaId));
        cast.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cast.Content.ReadAsStringAsync()).Should().Contain("This topic is closed. Voting is no longer allowed.");

        var del = await client.DeleteAsync($"/api/votes/{voteId}");
        del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await del.Content.ReadAsStringAsync()).Should().Contain("This topic is closed. Voting is no longer allowed.");
    }

    [Fact]
    public async Task OwnershipViolation_PreventsDeletingVote()
    {
        var ownerLogin = "voter1";
        var otherLogin = "voter2";
        var voter1 = new UserRecord { Id = Guid.NewGuid().ToString(), Login = ownerLogin, Name = ownerLogin, Password = "pw" };
        var voter2 = new UserRecord { Id = Guid.NewGuid().ToString(), Login = otherLogin, Name = otherLogin, Password = "pw" };
        var topicOwner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "topicOwner", Name = "topicOwner", Password = "pw" };
        var topicId = Guid.NewGuid().ToString();
        var ideaId = Guid.NewGuid();
        var voteId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Users.AddRange(voter1, voter2, topicOwner);
            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Topic A",
                Status = TopicStatus.OPEN,
                OwnerId = topicOwner.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Ideas.Add(new Idea
            {
                Id = ideaId,
                TopicId = topicId,
                OwnerId = Guid.Parse(topicOwner.Id),
                Title = "Idea 1",
                Description = "Desc",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Votes.Add(new Vote { Id = voteId, IdeaId = ideaId, UserId = Guid.Parse(voter1.Id) });
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenForLogin(otherLogin));

        var del = await client.DeleteAsync($"/api/votes/{voteId}");
        del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await del.Content.ReadAsStringAsync()).Should().Contain("You can only modify or delete your own vote.");
    }

    [Fact]
    public async Task DeletingIdea_DeletesRelatedVotes()
    {
        var ownerLogin = "ideaOwner";
        var owner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = ownerLogin, Name = ownerLogin, Password = "pw" };
        var other = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "voter", Name = "voter", Password = "pw" };
        var topicId = Guid.NewGuid().ToString();
        var ideaId = Guid.NewGuid();
        var voteId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Users.AddRange(owner, other);
            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Topic A",
                Status = TopicStatus.OPEN,
                OwnerId = owner.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Ideas.Add(new Idea
            {
                Id = ideaId,
                TopicId = topicId,
                OwnerId = Guid.Parse(owner.Id),
                Title = "Idea 1",
                Description = "Desc",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Votes.Add(new Vote { Id = voteId, IdeaId = ideaId, UserId = Guid.Parse(other.Id) });
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenForLogin(ownerLogin));

        var del = await client.DeleteAsync($"/api/ideas/{ideaId}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);
        (await del.Content.ReadAsStringAsync()).Should().Contain("All votes related to this idea were deleted as well");

        var votesLeft = await WithDbAsync(db => db.Votes.Where(v => v.IdeaId == ideaId).CountAsync());
        votesLeft.Should().Be(0);
    }

    [Fact]
    public async Task ClosingTopic_MarksWinningIdea_BasedOnMostVotes()
    {
        var topicOwnerLogin = "topicOwner";
        var topicOwner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = topicOwnerLogin, Name = topicOwnerLogin, Password = "pw" };
        var voterA = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "a", Name = "a", Password = "pw" };
        var voterB = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "b", Name = "b", Password = "pw" };

        var topicId = Guid.NewGuid().ToString();
        var idea1 = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.Parse(topicOwner.Id),
            Title = "Idea 1",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var idea2 = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.Parse(topicOwner.Id),
            Title = "Idea 2",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        await SeedAsync(db =>
        {
            db.Users.AddRange(topicOwner, voterA, voterB);
            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Topic A",
                Status = TopicStatus.OPEN,
                OwnerId = topicOwner.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Ideas.AddRange(idea1, idea2);

            // Idea2 wins with 2 votes
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = idea2.Id, UserId = Guid.Parse(voterA.Id) });
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = idea2.Id, UserId = Guid.Parse(voterB.Id) });
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = idea1.Id, UserId = Guid.Parse(voterA.Id) });
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenForLogin(topicOwnerLogin));

        var put = await client.PutAsJsonAsync($"/api/topics/{topicId}", new UpdateTopicRequest("Topic A", "Desc", "CLOSED"));
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var putBody = await put.Content.ReadFromJsonAsync<TopicResponse>();
        putBody.Should().NotBeNull();
        putBody!.WinningIdea.Should().NotBeNull();
        putBody.WinningIdea!.Title.Should().Be("Idea 2");

        var get = await client.GetFromJsonAsync<TopicResponse>($"/api/topics/{topicId}");
        get!.Status.Should().Be("CLOSED");
        get.WinningIdea.Should().NotBeNull();
        get.WinningIdea!.Title.Should().Be("Idea 2");
    }
}
