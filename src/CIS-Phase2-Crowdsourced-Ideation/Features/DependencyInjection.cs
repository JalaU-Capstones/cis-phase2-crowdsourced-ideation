using CIS.Phase2.CrowdsourcedIdeation.Features.Health;

namespace CIS.Phase2.CrowdsourcedIdeation.Features;

public static class DependencyInjection
{
    public static WebApplication MapFeatures(this WebApplication app)
    {
        app.MapHealthEndpoints();
        return app;
    }
}

