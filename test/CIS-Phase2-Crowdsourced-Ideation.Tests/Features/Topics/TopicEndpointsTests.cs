using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

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
            CreatedBy = Guid.NewGuid().ToString(),
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
            CreatedBy = Guid.NewGuid().ToString(),
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
}