using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CIS.Phase2.CrowdsourcedIdeation.Services;

public sealed record ExternalUserDto(string Id, string? Login, string? Name);

public interface IUserResolver
{
    Task<ExternalUserDto> GetByLoginAsync(string login, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves external (Phase 1) users by calling the Java User Management API.
/// This is used by V2 (MongoDB) flows when the JWT only contains a login (`sub`).
/// </summary>
public sealed class UserResolver(HttpClient http, IConfiguration configuration) : IUserResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ExternalUserDto> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("login is required.", nameof(login));

        var baseUrl = configuration["ExternalUserApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("ExternalUserApi:BaseUrl is not configured.");

        if (http.BaseAddress is null)
            http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

        var paths = configuration.GetSection("ExternalUserApi:ByLoginPaths").Get<string[]>();
        paths ??=
        [
            "/api/v2/users/by-login/{login}",
            "/api/users/by-login/{login}"
        ];

        var escapedLogin = Uri.EscapeDataString(login.Trim());

        foreach (var pathTemplate in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var path = pathTemplate.Replace("{login}", escapedLogin, StringComparison.OrdinalIgnoreCase);
            using var resp = await http.GetAsync(path, cancellationToken);

            if (resp.StatusCode == HttpStatusCode.NotFound)
                continue;

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<ExternalUserDto>(json, JsonOptions);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Id))
                throw new InvalidOperationException("External user API returned an invalid response.");

            // If the API doesn't echo login/name, keep what we have.
            return parsed with
            {
                Login = string.IsNullOrWhiteSpace(parsed.Login) ? login : parsed.Login,
                Name = string.IsNullOrWhiteSpace(parsed.Name) ? parsed.Login ?? login : parsed.Name
            };
        }

        throw new InvalidOperationException($"User '{login}' not found in external user API.");
    }
}

/// <summary>
/// Simple service-locator for scenarios where we can't inject <see cref="IUserResolver"/>
/// (because some services are constructed manually in endpoint code).
/// </summary>
public static class UserResolverAccessor
{
    public static IUserResolver? Current { get; set; }
}

