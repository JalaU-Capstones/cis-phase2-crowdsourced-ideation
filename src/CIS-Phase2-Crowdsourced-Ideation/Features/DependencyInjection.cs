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
        // Define common regex for versioning
        const string v1 = "v1";
        const string v2 = "v2";

        // V1 Endpoints
        var v1Group = app.MapGroup("/api").WithTags("V1");
        v1Group.MapTopicEndpoints(v1);
        v1Group.MapIdeaEndpoints(v1); 
        v1Group.MapVoteEndpoints(v1);
        v1Group.MapStatisticsEndpoints(v1);

        // V2 Endpoints
        var v2Group = app.MapGroup("/api").WithTags("V2");
        v2Group.MapTopicEndpoints(v2);
        v2Group.MapIdeaEndpoints(v2);
        v2Group.MapVoteEndpoints(v2);
        v2Group.MapStatisticsEndpoints(v2);

        return app;
    }
}
