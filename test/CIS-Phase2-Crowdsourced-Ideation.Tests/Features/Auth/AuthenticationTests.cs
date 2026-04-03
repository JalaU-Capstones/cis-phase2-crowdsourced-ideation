using System.Net;
using System.Net.Http.Headers;
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
                var signingKey = new SymmetricSecurityKey(
                    Convert.FromBase64String(Phase1SecretKey));

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
        var token  = TestHelpers.GenerateJwtToken(Phase1SecretKey, "testuser");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/topics");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: "a valid Phase-1-issued token must be accepted by the CIS API");
    }

    [Fact]
    public async Task GivenNoToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client   = _factory.CreateClient();
        var response = await client.GetAsync("/topics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "requests without a token must be rejected");
    }

    [Fact]
    public async Task GivenExpiredToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client       = _factory.CreateClient();
        var expiredToken = TestHelpers.GenerateJwtToken(
            Phase1SecretKey, username: "testuser", expiresInMinutes: -1);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/topics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "expired tokens must be rejected");
    }

    [Fact]
    public async Task GivenInvalidToken_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt");

        var response = await client.GetAsync("/topics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "malformed tokens must be rejected");
    }

    [Fact]
    public async Task GivenTokenSignedWithWrongSecret_WhenRequestingProtectedEndpoint_ThenReturns401()
    {
        var client      = _factory.CreateClient();
        var wrongSecret = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        var wrongToken  = TestHelpers.GenerateJwtToken(wrongSecret, "testuser");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", wrongToken);

        var response = await client.GetAsync("/topics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "tokens signed with an unknown key must be rejected");
    }
}