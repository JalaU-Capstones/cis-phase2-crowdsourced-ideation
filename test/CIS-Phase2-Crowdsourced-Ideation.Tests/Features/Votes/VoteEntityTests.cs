using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using FluentAssertions;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Votes;

public sealed class VoteEntityTests
{
    [Fact]
    public void DefaultId_IsNotEmpty()
    {
        new Vote().Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            IdeaId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        vote.Id.Should().NotBeEmpty();
        vote.IdeaId.Should().NotBeEmpty();
        vote.UserId.Should().NotBeEmpty();
    }
}

