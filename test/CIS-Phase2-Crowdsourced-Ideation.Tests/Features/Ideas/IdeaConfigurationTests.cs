using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Ideas;

public sealed class IdeaConfigurationTests
{
    [Fact]
    public void Configure_SetsCorrectMetadata()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new AppDbContext(options);
        
        var entityType = context.Model.FindEntityType(typeof(Idea));
        
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("ideas");
        
        var idProp = entityType!.FindProperty(nameof(Idea.Id));
        idProp!.GetColumnName().Should().Be("id");
        idProp!.GetMaxLength().Should().Be(36);

        entityType.FindProperty(nameof(Idea.TopicId))!.GetColumnName().Should().Be("topic_id");
        entityType.FindProperty(nameof(Idea.TopicId))!.GetMaxLength().Should().Be(36);
        entityType.FindProperty(nameof(Idea.OwnerId))!.GetColumnName().Should().Be("owner_id");
        entityType.FindProperty(nameof(Idea.OwnerId))!.GetMaxLength().Should().Be(36);
        entityType.FindProperty(nameof(Idea.Content))!.GetColumnName().Should().Be("content");

        // Relationship metadata (FKs).
        entityType.GetForeignKeys().Should().ContainSingle(fk =>
            fk.Properties.Any(p => p.Name == nameof(Idea.TopicId)));
    }
}
