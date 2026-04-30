// src/CIS-Phase2-Crowdsourced-Ideation/Features/Migration/MigrationServiceExtensions.cs
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using Microsoft.Extensions.Options;

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
        
        services.AddHttpClient(AutomatedMigrationWorker.HttpClientName, (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<MigrationSettings>>().Value;
            if (!string.IsNullOrWhiteSpace(settings.Phase1BaseUrl))
                client.BaseAddress = new Uri(settings.Phase1BaseUrl);
        });
        
        services.AddScoped<IMigrationService, MigrationService>();
        
        services.AddHostedService<AutomatedMigrationWorker>();

        return services;
    }
}