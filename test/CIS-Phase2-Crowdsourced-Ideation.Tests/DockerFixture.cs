using Testcontainers.MongoDb;
using Testcontainers.MySql;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests;

public sealed class DockerFixture : IAsyncLifetime
{
    public MySqlContainer MySql { get; } = new MySqlBuilder()
        .WithDatabase("sd3")
        .WithUsername("sd3user")
        .WithPassword("sd3pass")
        .WithImage("mysql:8.0")
        .Build();

    public MongoDbContainer Mongo { get; } = new MongoDbBuilder()
        .WithImage("mongo:6.0")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(MySql.StartAsync(), Mongo.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(MySql.StopAsync(), Mongo.StopAsync());
    }
}
