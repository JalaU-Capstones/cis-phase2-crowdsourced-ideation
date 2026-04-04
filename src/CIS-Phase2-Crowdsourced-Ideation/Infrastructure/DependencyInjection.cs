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
        
        // The secret key from Phase 1 (Java/Spring Boot) is a hex string.
        // It needs to be decoded as a byte array to be used as a SymmetricSecurityKey.
        var secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        // IMPORTANT: The secret key from Phase 1 is a hex string.
        // Convert the hex string to a byte array.
        var signingKeyBytes = Enumerable.Range(0, secretKey.Length / 2)
            .Select(x => Convert.ToByte(secretKey.Substring(x * 2, 2), 16))
            .ToArray();

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