using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class UserRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UserRepository _repo;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repo = new UserRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUser()
    {
        var user = new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var result = await _repo.GetByIdAsync("1");

        result.Should().NotBeNull();
        result!.Login.Should().Be("l1");
    }

    [Fact]
    public async Task GetByLoginAsync_WhenUserExists_ReturnsUser()
    {
        var user = new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var result = await _repo.GetByLoginAsync("l1");

        result.Should().NotBeNull();
        result!.Id.Should().Be("1");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        _context.Users.AddRange(
            new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" },
            new UserRecord { Id = "2", Login = "l2", Name = "N2", Password = "p" }
        );
        await _context.SaveChangesAsync();

        var result = await _repo.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddAsync_AddsUserToContext()
    {
        var user = new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" };
        await _repo.AddAsync(user);
        await _context.SaveChangesAsync();

        _context.Users.Should().Contain(user);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotThrow()
    {
        var user = new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var act = async () => await _repo.UpdateAsync(user);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_RemovesUserFromContext()
    {
        var user = new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        await _repo.DeleteAsync(user);
        await _context.SaveChangesAsync();

        _context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task ExistsAsync_WhenUserExists_ReturnsTrue()
    {
        var user = new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var result = await _repo.ExistsAsync("1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        _context.Users.AddRange(
            new UserRecord { Id = "1", Login = "l1", Name = "N1", Password = "p" },
            new UserRecord { Id = "2", Login = "l2", Name = "N2", Password = "p" }
        );
        await _context.SaveChangesAsync();

        var result = await _repo.CountAsync();

        result.Should().Be(2);
    }
}
