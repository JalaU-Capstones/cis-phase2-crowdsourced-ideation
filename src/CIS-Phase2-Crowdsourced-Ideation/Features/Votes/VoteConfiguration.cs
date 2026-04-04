using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Votes;

public sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("votes");
        
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id").HasMaxLength(36);
        
        builder.Property(v => v.IdeaId).HasColumnName("idea_id").IsRequired().HasMaxLength(36);
        builder.Property(v => v.UserId).HasColumnName("user_id").IsRequired().HasMaxLength(36);
        
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        
        // UNIQUE constraint: Un usuario solo puede votar una vez por idea
        builder.HasIndex(v => new { v.IdeaId, v.UserId })
            .IsUnique()
            .HasDatabaseName("ix_votes_idea_id_user_id");
    }
}