using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests;

public static class TestHelpers
{
    public static ClaimsPrincipal CreateClaimsPrincipal(string userId)
    {
        var claims   = new[] { new Claim("sub", userId) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
    
    public static string GenerateJwtToken(
        string hexSecret, // Expecting a hex encoded secret for consistency with Java
        string username,
        int expiresInMinutes = 60)
    {
        // Convert hex string to byte array as required for the SymmetricSecurityKey.
        var keyBytes = Enumerable.Range(0, hexSecret.Length / 2)
            .Select(x => Convert.ToByte(hexSecret.Substring(x * 2, 2), 16))
            .ToArray();
            
        var signingKey  = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(new[] { new Claim("sub", username) }),
            NotBefore          = now.AddMinutes(Math.Min(expiresInMinutes, 0)) .AddMinutes(-1),
            Expires            = now.AddMinutes(expiresInMinutes),
            IssuedAt           = now.AddMinutes(Math.Min(expiresInMinutes, 0)).AddMinutes(-1),
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(tokenDescriptor));
    }
}

public class TestRepositoryAdapter(AppDbContext context) : IRepositoryAdapter
{
    private readonly Lazy<ITopicRepository> _topics = new(() => new TopicRepository(context));
    private readonly Lazy<IIdeaRepository> _ideas = new(() => new IdeaRepository(context));
    private readonly Lazy<IVoteRepository> _votes = new(() => new VoteRepository(context));
    private readonly Lazy<IUserRepository> _users = new(() => new UserRepository(context));

    public ITopicRepository Topics => _topics.Value;
    public IIdeaRepository Ideas => _ideas.Value;
    public IVoteRepository Votes => _votes.Value;
    public IUserRepository Users => _users.Value;

    public Task SaveChangesAsync() => context.SaveChangesAsync();
}
