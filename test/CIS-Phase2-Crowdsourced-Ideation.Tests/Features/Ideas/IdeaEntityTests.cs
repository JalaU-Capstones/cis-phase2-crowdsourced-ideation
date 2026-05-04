using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Ideas;

public sealed class IdeaEntityTests
{
    [Fact]
    public void SettingTitle_UpdatesContentJson()
    {
        var idea = new Idea();
        idea.Title = "Hello";
        idea.Description = "World";
        idea.IsWinning = true;

        idea.Content.Should().Contain("\"title\":\"Hello\"");
        idea.Content.Should().Contain("\"description\":\"World\"");
        idea.Content.Should().Contain("\"isWinning\":true");
    }

    [Fact]
    public void SettingContent_InvalidJson_DoesNotThrow_AndDoesNotOverwriteFields()
    {
        var idea = new Idea { Title = "T", Description = "D", IsWinning = false };
        var act = () => idea.Content = "not-json";
        act.Should().NotThrow();

        idea.Title.Should().Be("T");
        idea.Description.Should().Be("D");
        idea.IsWinning.Should().BeFalse();
    }

    [Fact]
    public void SettingContent_DoubleEncodedJson_HydratesFields()
    {
        var inner = JsonSerializer.Serialize(new { title = "A", description = "B", isWinning = true });
        var doubleEncoded = JsonSerializer.Serialize(inner); // JSON string containing JSON object

        var idea = new Idea();
        idea.Content = doubleEncoded;

        idea.Title.Should().Be("A");
        idea.Description.Should().Be("B");
        idea.IsWinning.Should().BeTrue();
    }
}

