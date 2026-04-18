using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

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
        // The Java implementation uses Decoders.BASE64.decode(secretKey).
        // We must decode it from Base64 to get the actual key bytes.
        var secretKey = configuration["Jwt:SecretKey"]
            ?? Convert.ToBase64String(Encoding.UTF8.GetBytes("test-secret-key-test-secret-key-test-secret-key"));

        // IMPORTANT: The secret key from Phase 1 is Base64 encoded in the Java configuration.
        // We must decode it from Base64 to get the actual key bytes.
        var signingKeyBytes = Convert.FromBase64String(secretKey);
        var signingKey = new SymmetricSecurityKey(signingKeyBytes);

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

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = signingKey,
                    ValidateIssuer           = false, 
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero,
                    NameClaimType            = "sub"
                };

                options.Events = new JwtBearerEvents
                {
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
                Title   = "CIS Phase 2 - Crowdsourced Ideation API",
                Version = "v1"
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
        });

        return services;
    }
}
