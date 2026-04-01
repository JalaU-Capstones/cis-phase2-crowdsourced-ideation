using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Persistence;

public sealed class UserRecordConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        builder.ToTable("users");   
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").HasMaxLength(36);
        builder.Property(u => u.Login).HasColumnName("login").HasMaxLength(20);
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(u => u.Password).HasColumnName("password").HasMaxLength(100);
    }
}