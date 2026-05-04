using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;
using CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence.Repositories;
using CIS_Phase2_Crowdsourced_Ideation.Features.Votes;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace CIS.Phase2.CrowdsourcedIdeation.Tests.Infrastructure.Persistence.Repositories;

public sealed class VoteRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly VoteRepository _repo;

    public VoteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repo = new VoteRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll()
    {
        _context.Votes.AddRange(new Vote { Id = Guid.NewGuid() }, new Vote { Id = Guid.NewGuid() });
        await _context.SaveChangesAsync();

        var result = await _repo.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsVote()
    {
        var id = Guid.NewGuid();
        var vote = new Vote { Id = id };
        await _context.Votes.AddAsync(vote);
        await _context.SaveChangesAsync();

        var result = await _repo.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetByIdeaIdAsync_ReturnsMatchingVotes()
    {
        var ideaId = Guid.NewGuid();
        _context.Votes.AddRange(
            new Vote { Id = Guid.NewGuid(), IdeaId = ideaId },
            new Vote { Id = Guid.NewGuid(), IdeaId = Guid.NewGuid() }
        );
        await _context.SaveChangesAsync();

        var result = await _repo.GetByIdeaIdAsync(ideaId);

        result.Should().HaveCount(1);
        result.First().IdeaId.Should().Be(ideaId);
    }

    [Fact]
    public async Task ExistsAsync_WhenExists_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        await _context.Votes.AddAsync(new Vote { Id = id });
        await _context.SaveChangesAsync();

        var result = await _repo.ExistsAsync(id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CountByIdeaIdAsync_ReturnsCorrectCount()
    {
        var ideaId = Guid.NewGuid();
        _context.Votes.AddRange(
            new Vote { Id = Guid.NewGuid(), IdeaId = ideaId },
            new Vote { Id = Guid.NewGuid(), IdeaId = ideaId }
        );
        await _context.SaveChangesAsync();

        var result = await _repo.CountByIdeaIdAsync(ideaId);

        result.Should().Be(2);
    }

    [Fact]
    public async Task CountAsync_ReturnsTotalCount()
    {
        _context.Votes.AddRange(new Vote { Id = Guid.NewGuid() }, new Vote { Id = Guid.NewGuid() });
        await _context.SaveChangesAsync();

        var result = await _repo.CountAsync();

        result.Should().Be(2);
    }

    [Fact]
    public async Task AddAsync_AddsToContext()
    {
        var vote = new Vote { Id = Guid.NewGuid() };
        await _repo.AddAsync(vote);
        await _context.SaveChangesAsync();

        _context.Votes.Should().Contain(vote);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContext()
    {
        var id = Guid.NewGuid();
        var vote = new Vote { Id = id, IdeaId = Guid.NewGuid() };
        await _context.Votes.AddAsync(vote);
        await _context.SaveChangesAsync();

        var newIdeaId = Guid.NewGuid();
        vote.IdeaId = newIdeaId;
        await _repo.UpdateAsync(vote);
        await _context.SaveChangesAsync();

        var updated = await _context.Votes.FindAsync(id);
        updated!.IdeaId.Should().Be(newIdeaId);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromContext()
    {
        var id = Guid.NewGuid();
        var vote = new Vote { Id = id };
        await _context.Votes.AddAsync(vote);
        await _context.SaveChangesAsync();

        await _repo.DeleteAsync(vote);
        await _context.SaveChangesAsync();

        _context.Votes.Should().BeEmpty();
    }
}
