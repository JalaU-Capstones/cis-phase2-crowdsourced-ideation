using CIS.Phase2.CrowdsourcedIdeation.Features.Health;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

namespace CIS.Phase2.CrowdsourcedIdeation.Features;

public static class DependencyInjection
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddScoped<IIdeaService, IdeaService>();
        return services;
    }

    public static WebApplication MapFeatures(this WebApplication app)
    {
        app.MapHealthEndpoints();
        app.MapTopicEndpoints();
        app.MapIdeaEndpoints();
        return app;
    }
}