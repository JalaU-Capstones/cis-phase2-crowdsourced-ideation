using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using Microsoft.Extensions.Options;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Migration;

public sealed class AutomatedMigrationWorker : BackgroundService
{
    private readonly MigrationSettings _settings;
    private readonly MigrationStateManager _state;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomatedMigrationWorker> _logger;

    public AutomatedMigrationWorker(
        IOptions<MigrationSettings>       settings,
        MigrationStateManager             state,
        IServiceScopeFactory              scopeFactory,
        ILogger<AutomatedMigrationWorker> logger)
    {
        _settings     = settings.Value;
        _state        = state;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.RunOnStartup)
        {
            _logger.LogInformation("[Migration] RunOnStartup = false. Automated migration skipped.");
            return;
        }

        await RunMigrationAsync(stoppingToken);
    }

    public async Task RunMigrationAsync(CancellationToken ct)
    {
        Log("=== Automated C# Phase 2 Migration Starting ===");
        _state.SetMigrationRunning(true);

        try
        {
            Log("[Step 1/2] Running C# Phase 2 migration (topics, ideas, votes) …");
            MigrationResult result;

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
                result = await migrationService.RunAsync();
            }

            Log($"           Topics migrated : {result.MigratedTopics,8}");
            Log($"           Ideas  migrated : {result.MigratedIdeas,8}");
            Log($"           Votes  migrated : {result.MigratedVotes,8}");
            Log($"           Topics  MySQL={result.Validation.Topics.MySql,6}  MongoDB={result.Validation.Topics.Mongo,6}  Match={result.Validation.Topics.IsMatch}");
            Log($"           Ideas   MySQL={result.Validation.Ideas.MySql,6}  MongoDB={result.Validation.Ideas.Mongo,6}  Match={result.Validation.Ideas.IsMatch}");
            Log($"           Votes   MySQL={result.Validation.Votes.MySql,6}  MongoDB={result.Validation.Votes.Mongo,6}  Match={result.Validation.Votes.IsMatch}");

            if (!result.IsConsistent)
                throw new InvalidOperationException(
                    $"Migration validation failed – counts do not match. " +
                    $"Topics={result.Validation.Topics.MySql}/{result.Validation.Topics.Mongo}, " +
                    $"Ideas={result.Validation.Ideas.MySql}/{result.Validation.Ideas.Mongo}, " +
                    $"Votes={result.Validation.Votes.MySql}/{result.Validation.Votes.Mongo}");

            Log("[Step 1/2] Phase 2 migration complete – 100 % data consistency verified.");

            if (_settings.DowntimeSeconds > 0)
            {
                Log($"[Step 2/2] Waiting {_settings.DowntimeSeconds}s downtime window …");
                await Task.Delay(TimeSpan.FromSeconds(_settings.DowntimeSeconds), ct);
            }
            else
            {
                Log("[Step 2/2] DowntimeSeconds = 0 – skipping wait.");
            }

            _state.SetHasMigrated(true);
            Log("=== Migration completed. /api/v2/ is the primary API. /api/v1/ is permanently deprecated. ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{Ts}] === Migration FAILED: {Msg} — maintenance lock lifted, system reverts to dual-API state. ===",
                Ts, ex.Message);
        }
        finally
        {
            _state.SetMigrationRunning(false);
        }
    }

    private void Log(string message) => _logger.LogInformation("[{Ts}] {Msg}", Ts, message);
    private static string Ts => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
}