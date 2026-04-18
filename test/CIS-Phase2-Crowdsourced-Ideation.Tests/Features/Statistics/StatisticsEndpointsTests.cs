using CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Statistics;

public sealed class StatisticsEndpointsTests
{
    private static HttpContext CreateMockHttpContext()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        var context = new DefaultHttpContext();
        context.Items["RepositoryAdapter"] = adapter.Object;
        return context;
    }

    [Fact]
    public async Task TopTopics_ReturnsBadRequest_WhenLimitIsZero()
    {
        var http = CreateMockHttpContext();
        var res = await StatisticsEndpoints.HandleTopTopics(http, "v1", limit: 0, offset: 0);
        res.Should().BeOfType<BadRequest<ErrorResponse>>();
    }

    [Fact]
    public async Task MostVotedIdeas_ReturnsBadRequest_WhenOffsetIsNegative()
    {
        var http = CreateMockHttpContext();
        var res = await StatisticsEndpoints.HandleMostVotedIdeas(http, "v1", limit: 10, offset: -1);
        res.Should().BeOfType<BadRequest<ErrorResponse>>();
    }

    [Fact]
    public async Task TopicSummary_ReturnsBadRequest_WhenTopicIdIsEmpty()
    {
        var http = CreateMockHttpContext();
        var res = await StatisticsEndpoints.HandleTopicSummary(topicId: " ", http, "v1");
        res.Should().BeOfType<BadRequest<ErrorResponse>>();
    }

    [Fact]
    public async Task TopicSummary_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var adapter = new Mock<IRepositoryAdapter>();
        adapter.Setup(a => a.Topics.GetByIdAsync("t1")).ReturnsAsync((CIS.Phase2.CrowdsourcedIdeation.Features.Topics.Topic?)null);
        
        var http = new DefaultHttpContext();
        http.Items["RepositoryAdapter"] = adapter.Object;

        var res = await StatisticsEndpoints.HandleTopicSummary("t1", http, "v1");
        res.Should().BeOfType<NotFound>();
    }
}
