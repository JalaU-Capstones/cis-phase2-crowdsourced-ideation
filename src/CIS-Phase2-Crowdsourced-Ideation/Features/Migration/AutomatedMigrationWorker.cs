using CIS.Phase2.CrowdsourcedIdeation.Infrastructure;
using Microsoft.Extensions.Options;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Migration;

public sealed class AutomatedMigrationWorker : BackgroundService
{
    public const string HttpClientName = "JavaPhase1";

    private readonly MigrationSettings _settings;
    private readonly MigrationStateManager _state;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceScopeFactory _scopeFactory;   
    private readonly ILogger<AutomatedMigrationWorker> _logger;

    public AutomatedMigrationWorker(
        IOptions<MigrationSettings>       settings,
        MigrationStateManager             state,
        IHttpClientFactory                httpFactory,
        IServiceScopeFactory              scopeFactory,
        ILogger<AutomatedMigrationWorker> logger)
    {
        _settings     = settings.Value;
        _state        = state;
        _httpFactory  = httpFactory;
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
        Log("=== Automated ELT Migration Starting ===");
        _state.SetMigrationRunning(true);

        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);

            Log("[Step 1/4] Triggering Java Phase 1 user migration → POST /api/v1/system/migrate");
            var migrateResp = await client.PostAsync("/api/v1/system/migrate", content: null, ct);
            migrateResp.EnsureSuccessStatusCode();
            Log($"[Step 1/4] Java Phase 1 migration complete. HTTP {(int)migrateResp.StatusCode}");

            Log("[Step 2/4] Running C# Phase 2 migration (topics, ideas, votes) …");
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

            Log("[Step 2/4] Phase 2 migration complete – 100 % data consistency verified.");

            if (_settings.DowntimeSeconds > 0)
            {
                Log($"[Step 3/4] Waiting {_settings.DowntimeSeconds}s downtime window …");
                await Task.Delay(TimeSpan.FromSeconds(_settings.DowntimeSeconds), ct);
            }
            else
            {
                Log("[Step 3/4] DowntimeSeconds = 0 – skipping wait.");
            }

            Log("[Step 4/4] Sending sunset signal to Java Phase 1 → POST /api/v1/system/sunset");
            var sunsetResp = await client.PostAsync("/api/v1/system/sunset", content: null, ct);
            sunsetResp.EnsureSuccessStatusCode();
            Log($"[Step 4/4] Java Phase 1 sunset confirmed. HTTP {(int)sunsetResp.StatusCode}");

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