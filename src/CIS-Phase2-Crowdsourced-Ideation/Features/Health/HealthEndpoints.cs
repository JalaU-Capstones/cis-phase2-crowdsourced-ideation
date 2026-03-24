namespace CIS.Phase2.CrowdsourcedIdeation.Features.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Health")
            .WithTags("Health")
            .AllowAnonymous();

        return endpoints;
    }
}

