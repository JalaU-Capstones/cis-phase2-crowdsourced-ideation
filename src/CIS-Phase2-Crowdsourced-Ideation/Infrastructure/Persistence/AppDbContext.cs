using CIS.Phase2.CrowdsourcedIdeation.Features.Topics;
using CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;
using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<Idea> Ideas => Set<Idea>();
    public DbSet<Vote> Votes => Set<Vote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}