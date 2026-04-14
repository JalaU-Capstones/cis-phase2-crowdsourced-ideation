using CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Statistics;

public sealed class StatisticsEndpointsTests
{
    [Fact]
    public async Task TopTopics_ReturnsBadRequest_WhenLimitIsZero()
    {
        var svc = new Mock<IStatisticsService>(MockBehavior.Strict);
        var res = await StatisticsEndpoints.HandleTopTopics(limit: 0, offset: 0, svc.Object);
        res.Result.Should().BeOfType<BadRequest<ErrorResponse>>();
    }

    [Fact]
    public async Task MostVotedIdeas_ReturnsBadRequest_WhenOffsetIsNegative()
    {
        var svc = new Mock<IStatisticsService>(MockBehavior.Strict);
        var res = await StatisticsEndpoints.HandleMostVotedIdeas(limit: 10, offset: -1, svc.Object);
        res.Result.Should().BeOfType<BadRequest<ErrorResponse>>();
    }

    [Fact]
    public async Task TopicSummary_ReturnsBadRequest_WhenTopicIdIsEmpty()
    {
        var svc = new Mock<IStatisticsService>(MockBehavior.Strict);
        var res = await StatisticsEndpoints.HandleTopicSummary(topicId: " ", svc.Object);
        res.Result.Should().BeOfType<BadRequest<ErrorResponse>>();
    }

    [Fact]
    public async Task TopicSummary_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var svc = new Mock<IStatisticsService>(MockBehavior.Strict);
        svc.Setup(s => s.GetTopicSummaryAsync("t1")).ReturnsAsync((TopicSummaryDto?)null);

        var res = await StatisticsEndpoints.HandleTopicSummary("t1", svc.Object);
        res.Result.Should().BeOfType<NotFound>();
    }
}

