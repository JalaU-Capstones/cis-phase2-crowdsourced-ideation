using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using FluentAssertions;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Topics;

/// <summary>
/// Tests de integración para /api/v2/topics.
/// Usa FakeMongoDbAdapter (implementación directa de IRepositoryAdapter)
/// para evitar el error de proxy en MongoDbContext.
/// </summary>
public sealed class TopicsV2EndpointTests : IClassFixture<TopicsV2EndpointTests.V2ApiFactory>
{
    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    public sealed class V2ApiFactory : WebApplicationFactory<Program>
    {
        public Mock<ITopicRepository> TopicRepo { get; } = new();
        public Mock<IIdeaRepository>  IdeaRepo  { get; } = new();
        public Mock<IVoteRepository>  VoteRepo  { get; } = new();
        public Mock<IUserRepository>  UserRepo  { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=sd3;User=sd3user;Password=sd3pass;",
                    ["ConnectionStrings:MongoDbConnection"] = "mongodb://localhost:27017/sd3",
                    ["Persistence:Provider"]                = "MySQL",
                    ["Jwt:SecretKey"]                       = "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970",
                    ["Jwt:SecretKeyEncoding"]               = "hex",
                    ["Jwt:RequireHttpsMetadata"]            = "false"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // InMemory DB para v1
                var dbDescs = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                    .ToList();
                foreach (var d in dbDescs) services.Remove(d);
                services.AddDbContext<AppDbContext>(o =>
                    o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

                // Reemplaza MongoDbAdapter con TestMongoDbAdapter (repos mockeados)
                var mongoDesc = services.SingleOrDefault(d => d.ServiceType == typeof(MongoDbAdapter));
                if (mongoDesc is not null) services.Remove(mongoDesc);
                services.AddScoped<MongoDbAdapter>(_ =>
                    new TestMongoDbAdapter(TopicRepo.Object, IdeaRepo.Object, VoteRepo.Object, UserRepo.Object));

                // Fuerza la clave JWT correcta — igual que AuthenticationTests
                var secretKeyBytes = Enumerable
                    .Range(0, "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970".Length / 2)
                    .Select(x => Convert.ToByte("404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970".Substring(x * 2, 2), 16))
                    .ToArray();
                var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(secretKeyBytes);

                services.PostConfigureAll<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(options =>
                {
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
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
        }
    }

    // ---------------------------------------------------------------------------
    // TestMongoDbAdapter: hereda de MongoDbAdapter pero recibe repos mockeados
    // Necesita que MongoDbAdapter tenga un constructor alternativo o que
    // sobreescribamos las propiedades.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Adapter de test que implementa IRepositoryAdapter directamente
    /// y se registra en el lugar de MongoDbAdapter en el contenedor de DI.
    /// Como los endpoints hacen GetRequiredService[MongoDbAdapter](), esta clase
    /// hereda de MongoDbAdapter pasando un contexto dummy con strings vacíos.
    /// </summary>
    private sealed class TestMongoDbAdapter : MongoDbAdapter
    {
        private readonly ITopicRepository _topics;
        private readonly IIdeaRepository  _ideas;
        private readonly IVoteRepository  _votes;
        private readonly IUserRepository  _users;

        public TestMongoDbAdapter(
            ITopicRepository topics,
            IIdeaRepository  ideas,
            IVoteRepository  votes,
            IUserRepository  users)
            // MongoDbContext acepta connectionString y dbName — usamos uno que no conecta
            : base(new MongoDbContext("mongodb://localhost:27017", "test-never-connects"))
        {
            _topics = topics;
            _ideas  = ideas;
            _votes  = votes;
            _users  = users;
        }

        public override ITopicRepository Topics => _topics;
        public override IIdeaRepository  Ideas  => _ideas;
        public override IVoteRepository  Votes  => _votes;
        public override IUserRepository  Users  => _users;
    }

    // ---------------------------------------------------------------------------
    // Setup
    // ---------------------------------------------------------------------------

    private readonly V2ApiFactory _factory;

    public TopicsV2EndpointTests(V2ApiFactory factory)
    {
        _factory = factory;
        _factory.TopicRepo.Reset();
        _factory.IdeaRepo.Reset();
        _factory.VoteRepo.Reset();
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    // ---------------------------------------------------------------------------
    // GET /api/v2/topics/
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllTopicsV2_WhenNoTopics_Returns200WithEmptyList()
    {
        _factory.TopicRepo.Setup(r => r.GetFilteredAsync(null, null))
            .ReturnsAsync(new List<Topic>());
        _factory.TopicRepo.Setup(r => r.CountAsync()).ReturnsAsync(0);

        var response = await CreateClient().GetAsync("/api/v2/topics/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"data\"");
    }

    [Fact]
    public async Task GetAllTopicsV2_WhenTopicsExist_ReturnsTopicList()
    {
        var topic = new Topic
        {
            Id        = Guid.NewGuid().ToString(),
            Title     = "Mongo Topic",
            OwnerId   = Guid.NewGuid().ToString(),
            Status    = TopicStatus.OPEN,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.TopicRepo.Setup(r => r.GetFilteredAsync(null, null))
            .ReturnsAsync(new List<Topic> { topic });
        _factory.TopicRepo.Setup(r => r.CountAsync()).ReturnsAsync(1);

        var response = await CreateClient().GetAsync("/api/v2/topics/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Mongo Topic");
    }

    // ---------------------------------------------------------------------------
    // GET /api/v2/topics/{id}
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTopicByIdV2_WhenExists_Returns200()
    {
        var topicId = Guid.NewGuid().ToString();
        _factory.TopicRepo.Setup(r => r.GetByIdAsync(topicId))
            .ReturnsAsync(new Topic
            {
                Id        = topicId,
                Title     = "Found Topic",
                OwnerId   = Guid.NewGuid().ToString(),
                Status    = TopicStatus.OPEN,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var response = await CreateClient().GetAsync($"/api/v2/topics/{topicId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Found Topic");
    }

    [Fact]
    public async Task GetTopicByIdV2_WhenNotFound_Returns404()
    {
        var topicId = Guid.NewGuid().ToString();
        _factory.TopicRepo.Setup(r => r.GetByIdAsync(topicId))
            .ReturnsAsync((Topic?)null);

        var response = await CreateClient().GetAsync($"/api/v2/topics/{topicId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // POST /api/v2/topics/ (requiere auth)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateTopicV2_WithoutToken_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/v2/topics/",
            new { title = "New Topic", description = "Desc" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTopicV2_WithValidToken_Returns201()
    {
        var ownerId = Guid.NewGuid().ToString();

        // UserIdentityResolver busca el usuario por login (claim "sub") o lo crea
        _factory.UserRepo
            .Setup(r => r.GetByLoginAsync(It.IsAny<string>()))
            .ReturnsAsync(new CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.UserRecord
            {
                Id       = ownerId,
                Login    = ownerId,
                Name     = ownerId,
                Password = "x"
            });
        _factory.UserRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.UserRecord
            {
                Id       = ownerId,
                Login    = ownerId,
                Name     = ownerId,
                Password = "x"
            });

        _factory.TopicRepo
            .Setup(r => r.AddAsync(It.IsAny<Topic>()))
            .Returns(Task.CompletedTask);

        _factory.TopicRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new Topic
            {
                Id        = id,
                Title     = "New Topic",
                OwnerId   = ownerId,
                Status    = TopicStatus.OPEN,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var secretHex = "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970";
        var token     = TestHelpers.GenerateJwtToken(secretHex, ownerId);
        var client    = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/v2/topics/",
            new { title = "New Topic", description = "Desc" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/v2/topics/{id}
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteTopicV2_WhenNotOwner_Returns403()
    {
        var topicId     = Guid.NewGuid().ToString();
        var ownerId     = Guid.NewGuid().ToString();
        var requesterId = Guid.NewGuid().ToString(); // usuario distinto al owner

        _factory.TopicRepo.Setup(r => r.GetByIdAsync(topicId))
            .ReturnsAsync(new Topic
            {
                Id        = topicId,
                OwnerId   = ownerId,         // propietario real
                Status    = TopicStatus.OPEN,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        // UserIdentityResolver resolverá el requesterId del token
        _factory.UserRepo
            .Setup(r => r.GetByLoginAsync(It.IsAny<string>()))
            .ReturnsAsync(new CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.UserRecord
            {
                Id       = requesterId,
                Login    = requesterId,
                Name     = requesterId,
                Password = "x"
            });
        _factory.UserRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.UserRecord
            {
                Id       = requesterId,
                Login    = requesterId,
                Name     = requesterId,
                Password = "x"
            });

        var secretHex = "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970";
        var token     = TestHelpers.GenerateJwtToken(secretHex, requesterId);
        var client    = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/v2/topics/{topicId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTopicV2_WhenTopicNotFound_Returns404()
    {
        var topicId = Guid.NewGuid().ToString();
        _factory.TopicRepo.Setup(r => r.GetByIdAsync(topicId))
            .ReturnsAsync((Topic?)null);

        var secretHex = "404E635266556A586E3272357538782F413F4428472B4B6250645367566B5970";
        var token     = TestHelpers.GenerateJwtToken(secretHex, Guid.NewGuid().ToString());
        var client    = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/v2/topics/{topicId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // Aislamiento v1/v2
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task V1Endpoint_IsIsolatedFromV2_UsesDifferentAdapter()
    {
        // v1 usa InMemory (sin tocar el mock de Mongo)
        var v1Response = await CreateClient().GetAsync("/api/v1/topics/");
        v1Response.StatusCode.Should().Be(HttpStatusCode.OK,
            "v1 debe seguir funcionando con MySQL/InMemory");

        // v2 requiere el mock configurado
        _factory.TopicRepo.Setup(r => r.GetFilteredAsync(null, null))
            .ReturnsAsync(new List<Topic>());
        _factory.TopicRepo.Setup(r => r.CountAsync()).ReturnsAsync(0);

        var v2Response = await CreateClient().GetAsync("/api/v2/topics/");
        v2Response.StatusCode.Should().Be(HttpStatusCode.OK,
            "v2 debe funcionar con MongoDbAdapter mockeado");

        _factory.TopicRepo.Verify(
            r => r.GetFilteredAsync(It.IsAny<string?>(), It.IsAny<string?>()),
            Times.AtLeastOnce);
    }
}