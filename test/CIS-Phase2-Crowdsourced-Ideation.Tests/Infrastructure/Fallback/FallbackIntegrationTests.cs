using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Testcontainers.MongoDb;
using Testcontainers.MySql;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Fallback;

/// <summary>
/// End-to-end integration tests for the emergency fallback mechanism using real MySQL/MongoDB containers.
/// </summary>
[Collection("Docker")]
[Trait("Category", "DockerRequired")]
public sealed class FallbackIntegrationTests : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithDatabase("sd3")
        .WithUsername("sd3user")
        .WithPassword("sd3pass")
        .WithImage("mysql:8.0")
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:6.0")
        .Build();

    private string MysqlConnStr => _mysql.GetConnectionString();
    private string MongoConnStr => _mongo.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _mongo.StartAsync());
        await CreateMySqlSchemaAndSeedAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_mysql.StopAsync(), _mongo.StopAsync());
    }

    [Fact]
    public async Task WhenMongoDown_V2ReadsFallbackToMySql_V2WritesBlocked_V1StillWorks()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        // Stop MongoDB to force V2 fallback to MySQL.
        await _mongo.StopAsync();
        await WaitForFallbackWriteBlockAsync(client, "/api/v2/topics");

        await WaitFor503Or200Async(client, "/api/v2/topics/", expect503: false);

        // V2 read should still work (served from MySQL via fallback adapter).
        var v2Get = await client.GetAsync("/api/v2/topics/");
        var v2Body = await v2Get.Content.ReadAsStringAsync();
        v2Get.StatusCode.Should().Be(HttpStatusCode.OK, $"response body: {v2Body}");
        v2Body.Should().Contain("Fallback Topic");

        // V2 write should be blocked with the maintenance message (503).
        var v2Post = await PostAsAuthedAsync(client, "/api/v2/topics");
        v2Post.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await v2Post.Content.ReadAsStringAsync())
            .Should().Contain("Our system is currently undergoing planned maintenance.");

        // V1 read should work normally since MySQL is up.
        var v1Get = await client.GetAsync("/api/v1/topics/");
        v1Get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await v1Get.Content.ReadAsStringAsync()).Should().Contain("Fallback Topic");
    }

    [Fact]
    public async Task WhenBothDatabasesDown_AllRequestsReturnGenericOutageMessage()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        await Task.WhenAll(_mongo.StopAsync(), _mysql.StopAsync());
        await WaitFor503Or200Async(client, "/api/v1/topics/", expect503: true);

        var res = await client.GetAsync("/api/v1/topics/");
        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await res.Content.ReadAsStringAsync())
            .Should().Contain("Please try again later. Our maintenance team is working to resolve this issue.");
    }

    private static async Task WaitFor503Or200Async(HttpClient client, string path, bool expect503)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        HttpStatusCode? lastCode = null;
        string? lastBody = null;
        while (DateTime.UtcNow < deadline)
        {
            var res = await client.GetAsync(path);
            lastCode = res.StatusCode;
            lastBody = await res.Content.ReadAsStringAsync();
            if (expect503 && res.StatusCode == HttpStatusCode.ServiceUnavailable) return;
            if (!expect503 && res.StatusCode == HttpStatusCode.OK) return;
            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"Timed out waiting for {(expect503 ? "503" : "200")} from {path}. Last seen: {(int?)lastCode} {lastCode}. Body: {lastBody}");
    }

    private static async Task<HttpResponseMessage> PostAsAuthedAsync(HttpClient client, string path)
    {
        var token = TestHelpers.GenerateJwtToken(
            hexSecret: "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970",
            username: Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsync(path, new StringContent("{}", Encoding.UTF8, "application/json"));
    }

    private static async Task WaitForFallbackWriteBlockAsync(HttpClient client, string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline)
        {
            var res = await PostAsAuthedAsync(client, path);
            if (res.StatusCode == HttpStatusCode.ServiceUnavailable) return;
            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for fallback write-block (503) on {path}.");
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new FallbackApiFactory(NormalizeMySqlConn(MysqlConnStr), MongoConnStr);

    private static string NormalizeMySqlConn(string cs)
    {
        var b = new MySqlConnectionStringBuilder(cs)
        {
            AllowPublicKeyRetrieval = true,
            SslMode = MySqlSslMode.None,
            Pooling = false,
            ConnectionTimeout = 1,
            DefaultCommandTimeout = 1
        };
        return b.ConnectionString;
    }

    private sealed class FallbackApiFactory(string mysqlConn, string mongoConn) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                // Use container connection strings and fast health polling for the tests.
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = mysqlConn,
                    ["ConnectionStrings:MongoDbConnection"] = mongoConn,
                    ["Fallback:Enabled"] = "true",
                    ["Fallback:HealthCheckIntervalSeconds"] = "1",
                    ["Jwt:SecretKey"] = "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970",
                    ["Jwt:SecretKeyEncoding"] = "hex",
                    ["Jwt:RequireHttpsMetadata"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Ensure we use real MySQL (not in-memory).
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbDescriptor is not null) services.Remove(dbDescriptor);

                services.AddDbContext<AppDbContext>(o =>
                    o.UseMySql(mysqlConn, ServerVersion.AutoDetect(mysqlConn)));
            });
        }
    }

    private async Task CreateMySqlSchemaAndSeedAsync()
    {
        await using var conn = new MySqlConnection(NormalizeMySqlConn(MysqlConnStr));
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS users (
                                  id       VARCHAR(36) PRIMARY KEY,
                                  login    VARCHAR(20) NOT NULL UNIQUE,
                                  name     VARCHAR(200) NOT NULL,
                                  password VARCHAR(100) NOT NULL
                              );
                              CREATE TABLE IF NOT EXISTS topics (
                                  id          VARCHAR(36) PRIMARY KEY,
                                  title       VARCHAR(200) NOT NULL,
                                  description TEXT,
                                  status      VARCHAR(10) NOT NULL DEFAULT 'OPEN',
                                  owner_id    VARCHAR(36) NOT NULL,
                                  created_at  DATETIME NOT NULL,
                                  updated_at  DATETIME NOT NULL
                              );
                              CREATE TABLE IF NOT EXISTS ideas (
                                  id         VARCHAR(36) PRIMARY KEY,
                                  topic_id   VARCHAR(36) NOT NULL,
                                  owner_id   VARCHAR(36) NOT NULL,
                                  content    TEXT NOT NULL,
                                  created_at DATETIME NOT NULL,
                                  updated_at DATETIME NOT NULL
                              );
                              CREATE TABLE IF NOT EXISTS votes (
                                  id      VARCHAR(36) PRIMARY KEY,
                                  idea_id VARCHAR(36) NOT NULL,
                                  user_id VARCHAR(36) NOT NULL
                              );
                              """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Seed a minimal topic row for GET endpoints.
        var topicId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO users (id, login, name, password) VALUES (@id, @login, @name, @pwd)";
            cmd.Parameters.Add(new MySqlParameter("@id", userId));
            cmd.Parameters.Add(new MySqlParameter("@login", "seed"));
            cmd.Parameters.Add(new MySqlParameter("@name", "Seed"));
            cmd.Parameters.Add(new MySqlParameter("@pwd", "x"));
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                              INSERT INTO topics (id, title, description, status, owner_id, created_at, updated_at)
                              VALUES (@id, @title, @desc, 'OPEN', @owner, @created, @updated)
                              """;
            cmd.Parameters.Add(new MySqlParameter("@id", topicId));
            cmd.Parameters.Add(new MySqlParameter("@title", "Fallback Topic"));
            cmd.Parameters.Add(new MySqlParameter("@desc", "Seeded in MySQL"));
            cmd.Parameters.Add(new MySqlParameter("@owner", userId));
            cmd.Parameters.Add(new MySqlParameter("@created", now));
            cmd.Parameters.Add(new MySqlParameter("@updated", now));
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
