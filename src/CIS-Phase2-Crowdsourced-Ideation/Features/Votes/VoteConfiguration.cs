using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Votes;

public sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("votes");
        
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnName("id")
            .HasMaxLength(36)
            .HasConversion(v => v.ToString(), v => Guid.Parse(v));
        
        builder.Property(v => v.IdeaId)
            .HasColumnName("idea_id")
            .IsRequired()
            .HasMaxLength(36)
            .HasConversion(v => v.ToString(), v => Guid.Parse(v));

        builder.Property(v => v.UserId)
            .HasColumnName("user_id")
            .IsRequired()
            .HasMaxLength(36)
            .HasConversion(v => v.ToString(), v => Guid.Parse(v));
        
        // UNIQUE constraint: Un usuario solo puede votar una vez por idea
        builder.HasIndex(v => new { v.IdeaId, v.UserId })
            .IsUnique()
            .HasDatabaseName("uq_votes_idea_user");

        builder.HasOne(v => v.Idea)
            .WithMany(i => i.Votes)
            .HasForeignKey(v => v.IdeaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
