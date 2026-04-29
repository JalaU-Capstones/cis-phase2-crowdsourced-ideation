using System.Net;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using FluentAssertions;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Statistics;

public sealed class StatisticsV2EndpointTests : IClassFixture<StatisticsV2EndpointTests.StatV2Factory>
{
    // ---------------------------------------------------------------------------
    // TestMongoDbAdapter — reutilizado igual que en TopicsV2EndpointTests
    // ---------------------------------------------------------------------------

    private sealed class TestMongoDbAdapter : MongoDbAdapter
    {
        private readonly ITopicRepository _topics;
        private readonly IIdeaRepository  _ideas;
        private readonly IVoteRepository  _votes;
        private readonly IUserRepository  _users;

        public TestMongoDbAdapter(
            ITopicRepository topics, IIdeaRepository ideas,
            IVoteRepository votes,   IUserRepository users)
            : base(new MongoDbContext("mongodb://localhost:27017", "test-never-connects"))
        {
            _topics = topics; _ideas = ideas; _votes = votes; _users = users;
        }

        public override ITopicRepository Topics => _topics;
        public override IIdeaRepository  Ideas  => _ideas;
        public override IVoteRepository  Votes  => _votes;
        public override IUserRepository  Users  => _users;
    }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    public sealed class StatV2Factory : WebApplicationFactory<Program>
    {
        public Mock<ITopicRepository> TopicRepo { get; } = new();
        public Mock<IIdeaRepository>  IdeaRepo  { get; } = new();
        public Mock<IVoteRepository>  VoteRepo  { get; } = new();
        public Mock<IUserRepository>  UserRepo  { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"]  = "Server=localhost;Database=sd3;User=sd3user;Password=sd3pass;",
                    ["ConnectionStrings:MongoDbConnection"]  = "mongodb://localhost:27017/sd3",
                    ["Persistence:Provider"]                 = "MySQL",
                    ["Fallback:Enabled"]                     = "false",
                    ["Jwt:SecretKey"]                        = "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970",
                    ["Jwt:SecretKeyEncoding"]                = "hex",
                    ["Jwt:RequireHttpsMetadata"]             = "false"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                var dbDescs = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                    .ToList();
                foreach (var d in dbDescs) services.Remove(d);
                services.AddDbContext<AppDbContext>(o =>
                    o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

                var mongoDesc = services.SingleOrDefault(d => d.ServiceType == typeof(MongoDbAdapter));
                if (mongoDesc is not null) services.Remove(mongoDesc);
                services.AddScoped<MongoDbAdapter>(_ =>
                    new TestMongoDbAdapter(TopicRepo.Object, IdeaRepo.Object, VoteRepo.Object, UserRepo.Object));

                var secretKeyBytes = Enumerable
                    .Range(0, "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970".Length / 2)
                    .Select(x => Convert.ToByte("404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970".Substring(x * 2, 2), 16))
                    .ToArray();
                var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(secretKeyBytes);

                services.PostConfigureAll<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(options =>
                {
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey         = signingKey,
                        ValidateIssuer           = false,
                        ValidateAudience         = false,
                        ValidateLifetime         = true,
                        ClockSkew                = TimeSpan.Zero
                    };
                });
            });
        }
    }

    private readonly StatV2Factory _factory;

    public StatisticsV2EndpointTests(StatV2Factory factory)
    {
        _factory = factory;
        _factory.TopicRepo.Reset();
        _factory.IdeaRepo.Reset();
        _factory.VoteRepo.Reset();
    }

    // ---------------------------------------------------------------------------
    // GET /api/v2/statistics/top-topics
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTopTopicsV2_WhenNoData_Returns200WithEmptyList()
    {
        _factory.TopicRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Topic>());

        var response = await _factory.CreateClient().GetAsync("/api/v2/statistics/top-topics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task GetTopTopicsV2_WithLimit_RespectsLimit()
    {
        var topics = Enumerable.Range(1, 5).Select(i => new Topic
        {
            Id        = Guid.NewGuid().ToString(),
            Title     = $"Topic {i}",
            OwnerId   = Guid.NewGuid().ToString(),
            Status    = TopicStatus.OPEN,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        _factory.TopicRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(topics);
        _factory.IdeaRepo
            .Setup(r => r.GetByTopicIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Idea>());

        var response = await _factory.CreateClient()
            .GetAsync("/api/v2/statistics/top-topics?limit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = System.Text.Json.JsonSerializer.Deserialize<List<object>>(
            await response.Content.ReadAsStringAsync());
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTopTopicsV2_WithInvalidLimit_Returns400()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/v2/statistics/top-topics?limit=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------------------
    // GET /api/v2/statistics/most-voted-ideas
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetMostVotedIdeasV2_WhenNoIdeas_Returns200WithEmptyList()
    {
        _factory.IdeaRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Idea>());

        var response = await _factory.CreateClient()
            .GetAsync("/api/v2/statistics/most-voted-ideas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task GetMostVotedIdeasV2_WithNegativeOffset_Returns400()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/v2/statistics/most-voted-ideas?offset=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------------------
    // GET /api/v2/statistics/topic/{topicId}/summary
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTopicSummaryV2_WhenTopicNotFound_Returns404()
    {
        var topicId = Guid.NewGuid().ToString();
        _factory.TopicRepo.Setup(r => r.GetByIdAsync(topicId)).ReturnsAsync((Topic?)null);

        var response = await _factory.CreateClient()
            .GetAsync($"/api/v2/statistics/topic/{topicId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTopicSummaryV2_WhenTopicExists_ReturnsSummary()
    {
        var topicId = Guid.NewGuid().ToString();
        _factory.TopicRepo.Setup(r => r.GetByIdAsync(topicId))
            .ReturnsAsync(new Topic
            {
                Id        = topicId,
                Title     = "Summary Topic",
                OwnerId   = Guid.NewGuid().ToString(),
                Status    = TopicStatus.OPEN,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        _factory.IdeaRepo.Setup(r => r.GetByTopicIdAsync(topicId))
            .ReturnsAsync(new List<Idea>());

        var response = await _factory.CreateClient()
            .GetAsync($"/api/v2/statistics/topic/{topicId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Summary Topic");
        body.Should().Contain("\"ideasCount\":0");
        body.Should().Contain("\"votesCount\":0");
    }

    [Fact]
    public async Task GetTopicSummaryV2_WithIdeasAndVotes_ReturnsCorrectCounts()
    {
        var topicId = Guid.NewGuid().ToString();
        var ideaId  = Guid.NewGuid();

        _factory.TopicRepo.Setup(r => r.GetByIdAsync(topicId))
            .ReturnsAsync(new Topic
            {
                Id        = topicId,
                Title     = "Rich Topic",
                OwnerId   = Guid.NewGuid().ToString(),
                Status    = TopicStatus.OPEN,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        _factory.IdeaRepo.Setup(r => r.GetByTopicIdAsync(topicId))
            .ReturnsAsync(new List<Idea>
            {
                new()
                {
                    Id          = ideaId,
                    TopicId     = topicId,
                    OwnerId     = Guid.NewGuid(),
                    Title       = "Rich Idea",
                    Description = "Desc",
                    IsWinning   = true,
                    CreatedAt   = DateTime.UtcNow,
                    UpdatedAt   = DateTime.UtcNow
                }
            });

        _factory.VoteRepo.Setup(r => r.CountByIdeaIdAsync(ideaId)).ReturnsAsync(3);

        var response = await _factory.CreateClient()
            .GetAsync($"/api/v2/statistics/topic/{topicId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"ideasCount\":1");
        body.Should().Contain("\"votesCount\":3");
    }
}
