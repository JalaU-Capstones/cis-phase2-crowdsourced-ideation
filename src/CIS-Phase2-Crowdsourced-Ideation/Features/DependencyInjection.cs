using CIS.Phase2.CrowdsourcedIdeation.Features.Health;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Features.Votes;

namespace CIS.Phase2.CrowdsourcedIdeation.Features;

public static class DependencyInjection
{
    public static WebApplication MapFeatures(this WebApplication app)
    {
        app.MapHealthEndpoints();
        app.MapTopicEndpoints();
        app.MapVoteEndpoints();
        return app;
    }
}