using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

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
        string base64Secret,
        string username,
        int expiresInMinutes = 60)
    {
        var keyBytes    = Convert.FromBase64String(base64Secret);
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