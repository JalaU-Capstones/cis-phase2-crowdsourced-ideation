using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Auth;

public class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Phase1SecretKey =
        "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970";

    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbDescriptor is not null)
                    services.Remove(dbDescriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("AuthTestDb"));
                
                // Convert the hex string secret key to a byte array for the SymmetricSecurityKey
                var signingKeyBytes = Enumerable.Range(0, Phase1SecretKey.Length / 2)
                    .Select(x => Convert.ToByte(Phase1SecretKey.Substring(x * 2, 2), 16))
                    .ToArray();
                var signingKey = new SymmetricSecurityKey(signingKeyBytes);

                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey         = signingKey,
                        ValidateIssuer           = false,
                        ValidateAudience         = false,
                        ValidateLifetime         = true,
                        ClockSkew                = TimeSpan.Zero
                    };
                });
            });
        });
    }

    [Fact]
    public async Task GivenValidToken_WhenRequestingProtectedEndpoint_ThenAccessIsGranted()
    {
        var client = _factory.CreateClient();
        var token  = TestHelpers.GenerateJwtToken(Phase1SecretKey, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // POST is protected
        var response = await client.PostAsync("/api/v1/topics", null);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: "a valid Phase-1-issued token must be accepted by the CIS API");
    }

    [Fact]
    public async Task GivenNoToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client   = _factory.CreateClient();
        // POST is protected
        var response = await client.PostAsync("/api/v1/topics", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "requests without a token must be rejected");
    }

    [Fact]
    public async Task GivenExpiredToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client       = _factory.CreateClient();
        var expiredToken = TestHelpers.GenerateJwtToken(
            Phase1SecretKey, username: Guid.NewGuid().ToString(), expiresInMinutes: -1);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        // POST is protected
        var response = await client.PostAsync("/api/v1/topics", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "expired tokens must be rejected");
    }

    [Fact]
    public async Task GivenInvalidToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt");

        // POST is protected
        var response = await client.PostAsync("/api/v1/topics", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "malformed tokens must be rejected");
    }

    [Fact]
    public async Task GivenTokenSignedWithWrongSecret_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client      = _factory.CreateClient();
        // A different hex encoded secret for failure simulation
        var wrongSecret = "4141414141414141414141414141414141414141414141414141414141414141";
        var wrongToken  = TestHelpers.GenerateJwtToken(wrongSecret, Guid.NewGuid().ToString());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", wrongToken);

        // POST is protected
        var response = await client.PostAsync("/api/v1/topics", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "tokens signed with an unknown key must be rejected");
    }
}
