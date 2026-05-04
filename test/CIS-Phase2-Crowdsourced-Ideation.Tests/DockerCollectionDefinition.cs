using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests;

/// <summary>
/// Ensures Docker/Testcontainers-backed integration tests don't run in parallel with other tests.
/// This reduces flakiness caused by Docker resource contention (ports/CPU/IO).
/// </summary>
[CollectionDefinition("Docker", DisableParallelization = true)]
public sealed class DockerCollectionDefinition : ICollectionFixture<DockerFixture>;

