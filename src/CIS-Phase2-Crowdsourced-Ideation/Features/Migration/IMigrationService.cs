
namespace CIS.Phase2.CrowdsourcedIdeation.Features.Migration;

/// <summary>
/// Abstraction over the C# Phase 2 ELT migration logic (topics, ideas, votes).
/// Decouples AutomatedMigrationWorker from the concrete implementation,
/// enabling full unit-test coverage without real databases.
/// </summary>
public interface IMigrationService
{
    Task<MigrationResult> RunAsync();
}