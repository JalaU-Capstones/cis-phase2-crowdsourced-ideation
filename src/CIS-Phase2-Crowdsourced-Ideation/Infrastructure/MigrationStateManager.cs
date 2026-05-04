// src/CIS-Phase2-Crowdsourced-Ideation/Infrastructure/MigrationStateManager.cs
namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure;

/// <summary>
/// Singleton, thread-safe holder of the two migration phase flags.
///
/// Uses <see cref="Interlocked"/>  so reads
/// and writes are safe without locks even if multiple threads ever inspect
/// or set these flags concurrently
/// </summary>
public sealed class MigrationStateManager
{
    // 0 = false, 1 = true — Interlocked requires int/long
    private int _isMigrationRunning;
    private int _hasMigrated;

    /// <summary>True while the ELT worker is actively running.</summary>
    public bool IsMigrationRunning
        => Interlocked.CompareExchange(ref _isMigrationRunning, 0, 0) == 1;

    /// <summary>True once the migration has completed successfully.</summary>
    public bool HasMigrated
        => Interlocked.CompareExchange(ref _hasMigrated, 0, 0) == 1;

    public void SetMigrationRunning(bool value)
        => Interlocked.Exchange(ref _isMigrationRunning, value ? 1 : 0);

    public void SetHasMigrated(bool value)
        => Interlocked.Exchange(ref _hasMigrated, value ? 1 : 0);
}