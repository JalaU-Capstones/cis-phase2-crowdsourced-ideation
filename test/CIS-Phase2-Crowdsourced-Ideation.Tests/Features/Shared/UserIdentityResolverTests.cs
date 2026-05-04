using System.Security.Claims;
using CIS.Phase2.CrowdsourcedIdeation.Features.Shared;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Adapters;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Features.Shared;

public sealed class UserIdentityResolverTests
{
    private readonly Mock<IRepositoryAdapter> _adapterMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();

    public UserIdentityResolverTests()
    {
        _adapterMock.Setup(a => a.Users).Returns(_userRepoMock.Object);
    }

    private static ClaimsPrincipal CreateUser(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.type, c.value)), "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task ResolveOrProvisionUserIdAsync_WhenUserIdInSubClaim_ReturnsGuid()
    {
        var userId = Guid.NewGuid();
        var principal = CreateUser(("sub", userId.ToString()));
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);

        var result = await UserIdentityResolver.ResolveOrProvisionUserIdAsync(_adapterMock.Object, principal);

        result.Should().Be(userId);
    }

    [Fact]
    public async Task ResolveOrProvisionUserIdAsync_WhenUserNotFoundAndLoginExists_ReturnsExistingId()
    {
        var userId = Guid.NewGuid();
        var principal = CreateUser(("sub", "non-guid-login"));
        _userRepoMock.Setup(r => r.GetByLoginAsync("non-guid-login"))
            .ReturnsAsync(new UserRecord { Id = userId.ToString(), Login = "non-guid-login" });
        _userRepoMock.Setup(r => r.ExistsAsync(userId.ToString())).ReturnsAsync(true);

        var result = await UserIdentityResolver.ResolveOrProvisionUserIdAsync(_adapterMock.Object, principal);

        result.Should().Be(userId);
    }

    [Fact]
    public async Task ResolveOrProvisionUserIdAsync_WhenNoClaims_ThrowsUnauthorized()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var act = async () => await UserIdentityResolver.ResolveOrProvisionUserIdAsync(_adapterMock.Object, principal);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ResolveOrProvisionUserIdAsync_WhenUserDoesNotExist_ProvisionsNewUser()
    {
        var principal = CreateUser(("sub", "newuser"), ("name", "New User"));
        _userRepoMock.Setup(r => r.GetByLoginAsync("newuser")).ReturnsAsync((UserRecord?)null);
        _userRepoMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await UserIdentityResolver.ResolveOrProvisionUserIdAsync(_adapterMock.Object, principal);

        result.Should().NotBeEmpty();
        _userRepoMock.Verify(r => r.AddAsync(It.Is<UserRecord>(u => u.Login == "newuser" && u.Name == "New User")), Times.Once);
        _adapterMock.Verify(a => a.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ResolveOrProvisionUserIdAsync_MongoAdapter_WhenUserExists_ReturnsId()
    {
        var mongoAdapter = new Mock<MongoDbAdapter>(new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3").Object);
        mongoAdapter.Setup(a => a.Users).Returns(_userRepoMock.Object);
        
        var userId = Guid.NewGuid();
        var principal = CreateUser(("sub", "mongouser"));
        _userRepoMock.Setup(r => r.GetByLoginAsync("mongouser"))
            .ReturnsAsync(new UserRecord { Id = userId.ToString(), Login = "mongouser" });

        var result = await UserIdentityResolver.ResolveOrProvisionUserIdAsync(mongoAdapter.Object, principal);

        result.Should().Be(userId);
    }

    [Fact]
    public async Task ResolveOrProvisionUserIdAsync_MongoAdapter_WhenUserMissing_ThrowsUnauthorized()
    {
        var mongoAdapter = new Mock<MongoDbAdapter>(new Mock<MongoDbContext>("mongodb://localhost:27017", "sd3").Object);
        mongoAdapter.Setup(a => a.Users).Returns(_userRepoMock.Object);
        
        var principal = CreateUser(("sub", "missinguser"));
        _userRepoMock.Setup(r => r.GetByLoginAsync("missinguser")).ReturnsAsync((UserRecord?)null);

        var act = async () => await UserIdentityResolver.ResolveOrProvisionUserIdAsync(mongoAdapter.Object, principal);

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Phase 1 user migration*");
    }
}
