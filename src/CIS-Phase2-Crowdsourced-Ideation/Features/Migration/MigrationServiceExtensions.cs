using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Migration;

public static class MigrationServiceExtensions
{
    public static IServiceCollection AddMigrationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MigrationSettings>(
            configuration.GetSection(MigrationSettings.SectionName));

        services.AddSingleton<MigrationStateManager>();

        services.AddScoped<IMigrationService, MigrationService>();

        services.AddHostedService<AutomatedMigrationWorker>();

        return services;
    }
}