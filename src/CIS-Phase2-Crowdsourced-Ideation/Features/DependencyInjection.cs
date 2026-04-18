using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Statistics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;

namespace CIS.Phase2.CrowdsourcedIdeation.Features;

public static class DependencyInjection
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        // Dual Persistence: MySQL for V1, MongoDB for V2
        services.AddScoped<MySqlAdapter>();
        services.AddScoped<MongoDbAdapter>();

        // Register default services (fallback to MySQL if needed by other components)
        services.AddScoped<IRepositoryAdapter>(sp => sp.GetRequiredService<MySqlAdapter>());

        services.AddScoped<ITopicService, TopicService>();
        services.AddScoped<IIdeaService, IdeaService>();
        services.AddScoped<IVoteService, VoteService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        
        return services;
    }

    public static WebApplication MapFeatures(this WebApplication app)
    {
        // Map V1 endpoints (MySQL by default via filter in TopicEndpoints)
        app.MapTopicEndpoints("v1");
        
        // These currently don't have explicit versioning in MapIdeaEndpoints but they use /api/v1/ prefix in their implementation.
        // I will ensure they are consistent.
        app.MapIdeaEndpoints(); 
        app.MapVoteEndpoints();
        app.MapStatisticsEndpoints();

        // Map V2 endpoints (MongoDB via filter in TopicEndpoints)
        app.MapTopicEndpoints("v2");

        return app;
    }
}
