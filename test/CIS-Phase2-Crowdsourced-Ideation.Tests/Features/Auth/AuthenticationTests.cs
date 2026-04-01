using System.Net;
using System.Net.Http.Headers;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Auth;

/// <summary>
/// Integration tests for US 1.3 – Authentication for the CIS API.
/// Uses WebApplicationFactory with an in-memory DB so no real MySQL is required.
/// The JWT secret matches the Phase 1 application.yml secret key exactly.
/// </summary>
public class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Same Base64 secret declared in Phase 1 application.yml
    private const string Phase1SecretKey =
        "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970";

    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace MySQL with InMemory so tests are self-contained
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("AuthTestDb"));
            });
        });
    }

    // ── AC 1: valid token → access granted ───────────────────────────────────

    [Fact]
    public async Task GivenValidToken_WhenRequestingProtectedEndpoint_ThenAccessIsGranted()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token  = TestHelpers.GenerateJwtToken(Phase1SecretKey, "testuser");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/topics");

        // Assert – any 2xx or 4xx (like 404/400) is fine; 401 is NOT acceptable
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: "a valid Phase-1-issued token must be accepted by the CIS API");
    }

    // ── AC 2a: no token → 401 ────────────────────────────────────────────────

    [Fact]
    public async Task GivenNoToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/topics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "requests without a token must be rejected");
    }

    // ── AC 2b: expired token → 401 ───────────────────────────────────────────

    [Fact]
    public async Task GivenExpiredToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var expiredToken = TestHelpers.GenerateJwtToken(
            Phase1SecretKey,
            username: "testuser",
            expiresInMinutes: -1); // already expired

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.GetAsync("/topics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "expired tokens must be rejected");
    }

    // ── AC 2c: invalid token → 401 ───────────────────────────────────────────

    [Fact]
    public async Task GivenInvalidToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt");

        // Act
        var response = await client.GetAsync("/topics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "malformed tokens must be rejected");
    }

    // ── AC 2d: token signed with a different secret → 401 ────────────────────

    [Fact]
    public async Task GivenTokenSignedWithWrongSecret_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        // Arrange
        var client      = _factory.CreateClient();
        var wrongSecret = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="; // valid Base64, wrong key
        var wrongToken  = TestHelpers.GenerateJwtToken(wrongSecret, "testuser");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", wrongToken);

        // Act
        var response = await client.GetAsync("/topics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "tokens signed with an unknown key must be rejected");
    }
}