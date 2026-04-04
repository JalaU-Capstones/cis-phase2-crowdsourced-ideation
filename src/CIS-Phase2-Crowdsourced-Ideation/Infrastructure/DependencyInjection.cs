using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
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
        
        // The secret key from Phase 1 (Java/Spring Boot) is expected to be a Base64 encoded string.
        // The Java JwtService uses `Decoders.BASE64.decode(secretKey)`.
        var secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        // Convert the Base64 string secret key to bytes.
        var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(secretKey));

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
                    ValidateIssuer           = false, // Set to true if Phase 1 provides an issuer
                    ValidateAudience         = false, // Set to true if Phase 1 provides an audience
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero 
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