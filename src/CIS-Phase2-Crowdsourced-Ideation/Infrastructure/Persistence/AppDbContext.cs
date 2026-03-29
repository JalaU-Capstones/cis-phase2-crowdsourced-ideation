using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Topic> Topics => Set<Topic>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}