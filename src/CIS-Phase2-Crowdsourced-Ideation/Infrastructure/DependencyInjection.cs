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
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
            
        // Register persistence adapter based on config
        var provider = configuration["Persistence:Provider"] ?? "MySQL";
        if (provider.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IRepositoryAdapter, MySqlAdapter>();
        }
        else if (provider.Equals("MongoDB", StringComparison.OrdinalIgnoreCase))
        {
            var mongoConnection = configuration.GetConnectionString("MongoDbConnection")
                ?? throw new InvalidOperationException("Connection string 'MongoDbConnection' was not found.");
            services.AddSingleton(new MongoDbContext(mongoConnection, "sd3")); // Assuming database name sd3
            services.AddScoped<IRepositoryAdapter, MongoDbAdapter>();
        }
        else
        {
            throw new InvalidOperationException($"Unsupported persistence provider: {provider}");
        }

        // The secret key from Phase 1 (Java/Spring Boot) is configured in appsettings.json.
        // The Java implementation uses Decoders.BASE64.decode(secretKey).
        // We must decode it from Base64 to get the actual key bytes.
        var secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

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