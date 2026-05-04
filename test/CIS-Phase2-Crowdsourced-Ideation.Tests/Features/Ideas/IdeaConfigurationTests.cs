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
    }
}
