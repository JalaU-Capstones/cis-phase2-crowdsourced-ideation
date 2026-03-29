using System.Security.Claims;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests;

public static class TestHelpers
{
    public static ClaimsPrincipal CreateClaimsPrincipal(string userId)
    {
        var claims = new[] { new Claim("sub", userId) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}