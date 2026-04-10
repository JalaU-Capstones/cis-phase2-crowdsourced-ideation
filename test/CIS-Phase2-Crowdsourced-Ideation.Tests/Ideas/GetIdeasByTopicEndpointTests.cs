using System.Net;
using System.Net.Http.Json;
using System.Text;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Ideas;

public sealed class GetIdeasByTopicEndpointTests
{
    private sealed class ApiFactory(string dbName) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                // AddInfrastructure() requires these keys at startup; the DB connection isn't used since we replace DbContext.
                var jwtKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-secret-key-test-secret-key"));
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=sd3;User=sd3user;Password=sd3pass;",
                    ["Jwt:SecretKey"] = jwtKey,
                    ["Jwt:RequireHttpsMetadata"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                var dbContextOptions = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                    .ToList();
                foreach (var descriptor in dbContextOptions)
                    services.Remove(descriptor);

                var dbContext = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
                if (dbContext is not null)
                    services.Remove(dbContext);

                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
            });
        }
    }

    [Fact]
    public async Task GetIdeasByTopic_ReturnsEmptyArray_WhenTopicDoesNotExist()
    {
        var topicId = Guid.NewGuid().ToString();

        await using var factory = new ApiFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/ideas/topic/{topicId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IdeaResponse[]>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetIdeasByTopic_ReturnsIdeas_WithTitleAndDescriptionHydratedFromContentJson()
    {
        var dbName = Guid.NewGuid().ToString();
        var topicId = Guid.NewGuid().ToString();
        var ideaId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var createdAt = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc);

        await using var factory = new ApiFactory(dbName);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            db.Topics.Add(new Topic
            {
                Id = topicId,
                Title = "Seed Topic",
                Description = null,
                Status = TopicStatus.OPEN,
                OwnerId = Guid.NewGuid().ToString(),
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            });

            // Set Content directly to prove the API hydrates Title/Description from the legacy JSON column.
            db.Ideas.Add(new Idea
            {
                Id = ideaId,
                TopicId = topicId,
                OwnerId = ownerId,
                Content = "{\"title\":\"My Title\",\"description\":\"My Desc\",\"isWinning\":true}",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            });

            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var ideas = await client.GetFromJsonAsync<IdeaResponse[]>($"/api/ideas/topic/{topicId}");

        ideas.Should().NotBeNull();
        ideas!.Should().HaveCount(1);
        var idea = ideas!.Single();
        idea.Should().Be(new IdeaResponse(
            ideaId,
            topicId,
            ownerId,
            "My Title",
            "My Desc",
            createdAt,
            updatedAt,
            true
        ));
    }
}
