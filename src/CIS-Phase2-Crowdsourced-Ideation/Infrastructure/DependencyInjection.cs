using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Reflection;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure;

/// <summary>
/// Contains extension methods for registering infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure-related services to the container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Port=3307;Database=sd3;User Id=sd3user;Password=sd3pass;SslMode=None;AllowPublicKeyRetrieval=true;";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (connectionString.Contains("Server=localhost") || connectionString.Contains("Database=sd3"))
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }
            else
            {
                // Fallback for tests or other environments if needed, but usually we replace this in tests.
                options.UseInMemoryDatabase("sd3-fallback");
            }
        });

        // Always register MongoDB for V2 dual persistence
        var mongoConnection = configuration.GetConnectionString("MongoDbConnection") ?? "mongodb://localhost:27017";
        services.AddSingleton(new MongoDbContext(mongoConnection, "sd3"));
        services.AddScoped<MongoDbAdapter>();

        // Always register MySQL for V1
        services.AddScoped<MySqlAdapter>();
            
        // Register default persistence adapter based on config
        var provider = configuration["Persistence:Provider"] ?? "MySQL";
        if (provider.Equals("MongoDB", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IRepositoryAdapter>(sp => sp.GetRequiredService<MongoDbAdapter>());
        }
        else
        {
            services.AddScoped<IRepositoryAdapter>(sp => sp.GetRequiredService<MySqlAdapter>());
        }

        // The secret key from Phase 1 (Java/Spring Boot) is configured in appsettings.json.
        // Different Phase 1 setups represent the same HMAC key differently:
        // - raw string (e.g. "mySecretKey123")
        // - Base64 encoded string
        // - hex encoded string (common in tests)
        var secretKey = configuration["Jwt:SecretKey"]
            ?? "test-secret-key-test-secret-key-test-secret-key";

        var signingKeyBytes = DecodeJwtSecret(secretKey);
        var signingKey = new SymmetricSecurityKey(signingKeyBytes);

        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata =
                    configuration.GetValue("Jwt:RequireHttpsMetadata", false);
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = signingKey,
                    ValidateIssuer           = !string.IsNullOrWhiteSpace(issuer),
                    ValidIssuer              = issuer,
                    ValidateAudience         = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience            = audience,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero,
                    NameClaimType            = "sub"
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // Ensure consumers can rely on ClaimTypes.NameIdentifier being present.
                        var identity = context.Principal?.Identity as ClaimsIdentity;
                        if (identity is not null && !identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                        {
                            var sub = identity.FindFirst("sub")?.Value;
                            if (!string.IsNullOrWhiteSpace(sub))
                                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, sub));
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        return context.Response.WriteAsync("{\"error\": \"Unauthorized - Valid token required\"}");
                    },
                    OnForbidden = context =>
                    {
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        return context.Response.WriteAsync("{\"error\": \"You are not authorized to modify this topic\"}");
                    }
                };
            });

        services.AddAuthorization();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title   = "CIS Phase 2 - Crowdsourced Ideation API (V1)",
                Version = "v1"
            });
            
            c.SwaggerDoc("v2", new OpenApiInfo
            {
                Title   = "CIS Phase 2 - Crowdsourced Ideation API (V2 - Experimental)",
                Version = "v2"
            });

            // Ensure each document only contains its own versioned endpoints.
            c.DocInclusionPredicate((docName, apiDesc) =>
            {
                var rel = apiDesc.RelativePath ?? string.Empty;
                rel = rel.Split('?', 2)[0].TrimStart('/');
                return docName switch
                {
                    "v1" => rel.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase),
                    "v2" => rel.StartsWith("api/v2/", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name        = "Authorization",
                Type        = SecuritySchemeType.Http,
                Scheme      = "bearer",
                BearerFormat = "JWT",
                In          = ParameterLocation.Header,
                Description = "JWT Bearer token (delegated from Phase 1 User Management API).",
                Reference   = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            };

            c.AddSecurityDefinition("Bearer", scheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [scheme] = Array.Empty<string>()
            });

            // Enrich Swagger with XML comments from the codebase.
            var xmlName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });

        return services;
    }

    private static byte[] DecodeJwtSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return Encoding.UTF8.GetBytes("test-secret-key-test-secret-key-test-secret-key");

        var s = secret.Trim();

        // Prefer hex detection first because a hex string can also be valid Base64 characters.
        if (LooksLikeHex(s))
        {
            try { return DecodeHex(s); }
            catch (FormatException) { /* fall through */ }
        }

        try
        {
            return Convert.FromBase64String(s);
        }
        catch (FormatException)
        {
            // Not Base64, treat as raw.
            return Encoding.UTF8.GetBytes(s);
        }
    }

    private static bool LooksLikeHex(string s)
    {
        if (s.Length < 2 || (s.Length % 2) != 0)
            return false;

        foreach (var c in s)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }
        return true;
    }

    private static byte[] DecodeHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return bytes;
    }
}
