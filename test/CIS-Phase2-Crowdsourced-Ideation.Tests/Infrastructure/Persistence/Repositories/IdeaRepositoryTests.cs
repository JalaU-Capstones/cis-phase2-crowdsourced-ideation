using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class IdeaRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IdeaRepository _repo;

    public IdeaRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repo = new IdeaRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsIdea()
    {
        var id = Guid.NewGuid();
        await _context.Ideas.AddAsync(new Idea { Id = id, TopicId = "t1", OwnerId = Guid.NewGuid(), Title = "T", Description = "D" });
        await _context.SaveChangesAsync();

        var found = await _repo.GetByIdAsync(id);
        found.Should().NotBeNull();
        found!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetByTopicIdAsync_Filters()
    {
        _context.Ideas.AddRange(
            new Idea { Id = Guid.NewGuid(), TopicId = "t1", OwnerId = Guid.NewGuid(), Title = "A", Description = "D" },
            new Idea { Id = Guid.NewGuid(), TopicId = "t2", OwnerId = Guid.NewGuid(), Title = "B", Description = "D" }
        );
        await _context.SaveChangesAsync();

        (await _repo.GetByTopicIdAsync("t1")).Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_DoesNotThrow()
    {
        var act = async () => await _repo.DeleteAsync(new Idea { Id = Guid.NewGuid() });
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenMissing()
    {
        (await _repo.ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }
}

