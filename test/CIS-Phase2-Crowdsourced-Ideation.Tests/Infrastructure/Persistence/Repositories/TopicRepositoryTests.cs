using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class TopicRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TopicRepository _repo;

    public TopicRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repo = new TopicRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetFilteredAsync_StatusFilter_InvalidStatus_IsIgnored()
    {
        _context.Topics.Add(new Topic { Id = "t1", Title = "T1", Status = TopicStatus.OPEN, OwnerId = "u1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _repo.GetFilteredAsync(status: "not-a-status", ownerId: null);
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetFilteredAsync_StatusAndOwner_Filters()
    {
        _context.Topics.AddRange(
            new Topic { Id = "t1", Title = "T1", Status = TopicStatus.OPEN, OwnerId = "u1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Topic { Id = "t2", Title = "T2", Status = TopicStatus.CLOSED, OwnerId = "u1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Topic { Id = "t3", Title = "T3", Status = TopicStatus.CLOSED, OwnerId = "u2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        var result = await _repo.GetFilteredAsync(status: "CLOSED", ownerId: "u1");
        result.Should().ContainSingle().Which.Id.Should().Be("t2");
    }

    [Fact]
    public async Task DeleteAsync_MarksEntityDeleted()
    {
        var t = new Topic { Id = "t1", Title = "T1", Status = TopicStatus.OPEN, OwnerId = "u1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Topics.Add(t);
        await _context.SaveChangesAsync();

        await _repo.DeleteAsync(t);
        await _context.SaveChangesAsync();

        (await _repo.ExistsAsync("t1")).Should().BeFalse();
    }
}

