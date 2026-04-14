using System.Net;
using System.Net.Http.Json;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Statistics;

public sealed class StatisticsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName = $"StatsTestDb-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _dbRoot = new();

    public StatisticsApiTests(WebApplicationFactory<Program> factory)
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
            });
        });
    }

    private async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TopTopics_ReturnsEmpty_WhenNoData()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/statistics/top-topics");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<List<TopTopicDto>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("-1", "0")]
    [InlineData("10", "-1")]
    public async Task TopTopics_Returns400_ForInvalidPaging(string limit, string offset)
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/statistics/top-topics?limit={limit}&offset={offset}");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MostVotedIdeas_ReturnsEmpty_WhenNoData()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/statistics/most-voted-ideas");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<List<MostVotedIdeaDto>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("-1", "0")]
    [InlineData("10", "-1")]
    public async Task MostVotedIdeas_Returns400_ForInvalidPaging(string limit, string offset)
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/statistics/most-voted-ideas?limit={limit}&offset={offset}");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TopicSummary_Returns404_WhenTopicDoesNotExist()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/statistics/topic/{Guid.NewGuid()}/summary");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TopicSummary_Returns200_WithCounts_AndWinningIdeaIfPresent()
    {
        var topicId = Guid.NewGuid().ToString();
        var owner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "owner", Name = "owner", Password = "pw" };
        var voterA = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "a", Name = "a", Password = "pw" };

        var idea1 = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.Parse(owner.Id),
            Title = "Idea 1",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
            IsWinning = false
        };
        var idea2 = new Idea
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            OwnerId = Guid.Parse(owner.Id),
            Title = "Idea 2",
            Description = "Desc",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            IsWinning = true
        };

        await SeedAsync(db =>
        {
            db.Users.AddRange(owner, voterA);
            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Topic A",
                Status = TopicStatus.CLOSED,
                OwnerId = owner.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Ideas.AddRange(idea1, idea2);
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = idea2.Id, UserId = Guid.Parse(voterA.Id) });
        });

        var client = _factory.CreateClient();
        var res = await client.GetAsync($"/api/statistics/topic/{topicId}/summary");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<TopicSummaryDto>();
        body.Should().NotBeNull();
        body!.TopicId.Should().Be(topicId);
        body.IdeasCount.Should().Be(2);
        body.VotesCount.Should().Be(1);
        body.WinningIdea.Should().NotBeNull();
        body.WinningIdea!.IdeaTitle.Should().Be("Idea 2");
        body.MostVotedIdea.Should().NotBeNull();
        body.MostVotedIdea!.IdeaTitle.Should().Be("Idea 2");
    }

    [Fact]
    public async Task TopTopics_OrdersByVotesCount_AndPaginates()
    {
        var owner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "owner", Name = "owner", Password = "pw" };
        var voter = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "v", Name = "v", Password = "pw" };

        var t1 = Guid.NewGuid().ToString();
        var t2 = Guid.NewGuid().ToString();
        var t3 = Guid.NewGuid().ToString();

        var i11 = new Idea { Id = Guid.NewGuid(), TopicId = t1, OwnerId = Guid.Parse(owner.Id), Title = "t1-i1", Description = "d", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var i21 = new Idea { Id = Guid.NewGuid(), TopicId = t2, OwnerId = Guid.Parse(owner.Id), Title = "t2-i1", Description = "d", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var i31 = new Idea { Id = Guid.NewGuid(), TopicId = t3, OwnerId = Guid.Parse(owner.Id), Title = "t3-i1", Description = "d", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        await SeedAsync(db =>
        {
            db.Users.AddRange(owner, voter);
            db.Topics.AddRange(
                new Topic { Id = t1, Title = "T1", Status = TopicStatus.OPEN, OwnerId = owner.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Topic { Id = t2, Title = "T2", Status = TopicStatus.OPEN, OwnerId = owner.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Topic { Id = t3, Title = "T3", Status = TopicStatus.OPEN, OwnerId = owner.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            db.Ideas.AddRange(i11, i21, i31);

            // Votes: t2 -> 2 votes, t1 -> 1 vote, t3 -> 0 votes
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = i21.Id, UserId = Guid.Parse(voter.Id) });
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = i21.Id, UserId = Guid.NewGuid() });
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = i11.Id, UserId = Guid.NewGuid() });
        });

        var client = _factory.CreateClient();
        var all = await client.GetFromJsonAsync<List<TopTopicDto>>("/api/statistics/top-topics?limit=10&offset=0");
        all!.Select(x => x.TopicTitle).Should().ContainInOrder("T2", "T1", "T3");

        var page = await client.GetFromJsonAsync<List<TopTopicDto>>("/api/statistics/top-topics?limit=1&offset=1");
        page.Should().NotBeNull();
        var p = page!;
        p.Should().HaveCount(1);
        p[0].TopicTitle.Should().Be("T1");
    }

    [Fact]
    public async Task MostVotedIdeas_OrdersByVotes_AndIncludesTopic()
    {
        var owner = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "owner2", Name = "owner2", Password = "pw" };
        var voter = new UserRecord { Id = Guid.NewGuid().ToString(), Login = "v2", Name = "v2", Password = "pw" };
        var topicId = Guid.NewGuid().ToString();

        var idea1 = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = Guid.Parse(owner.Id), Title = "Idea A", Description = "d", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var idea2 = new Idea { Id = Guid.NewGuid(), TopicId = topicId, OwnerId = Guid.Parse(owner.Id), Title = "Idea B", Description = "d", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        await SeedAsync(db =>
        {
            db.Users.AddRange(owner, voter);
            db.Topics.Add(new Topic { Id = topicId, Title = "Topic X", Status = TopicStatus.OPEN, OwnerId = owner.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.Ideas.AddRange(idea1, idea2);
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), IdeaId = idea2.Id, UserId = Guid.Parse(voter.Id) });
        });

        var client = _factory.CreateClient();
        var data = await client.GetFromJsonAsync<List<MostVotedIdeaDto>>("/api/statistics/most-voted-ideas?limit=10&offset=0");
        data.Should().NotBeNull();
        var d = data!;
        d.Should().HaveCount(2);
        d[0].IdeaTitle.Should().Be("Idea B");
        d[0].TopicTitle.Should().Be("Topic X");
    }
}
