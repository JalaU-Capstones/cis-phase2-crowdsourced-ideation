using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

namespace CIS.Phase2.CrowdsourcedIdeation.Features;

public static class DependencyInjection
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        // Register services (they internally use the injected adapter)
        services.AddScoped<ITopicService, TopicService>();
        services.AddScoped<IIdeaService, IdeaService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        
        return services;
    }

    public static WebApplication MapFeatures(this WebApplication app)
    {
        // V1 Endpoints
        app.MapTopicEndpoints("v1");
        app.MapIdeaEndpoints("v1"); 
        app.MapVoteEndpoints("v1");
        app.MapStatisticsEndpoints("v1");

        // V2 Endpoints
        app.MapTopicEndpoints("v2");
        app.MapIdeaEndpoints("v2");
        app.MapVoteEndpoints("v2");
        app.MapStatisticsEndpoints("v2");

        return app;
    }
}
