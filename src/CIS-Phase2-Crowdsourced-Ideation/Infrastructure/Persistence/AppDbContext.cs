using Microsoft.EntityFrameworkCore;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;

// EF Core is wired and ready. Entities and mappings will be added per feature slices.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}

