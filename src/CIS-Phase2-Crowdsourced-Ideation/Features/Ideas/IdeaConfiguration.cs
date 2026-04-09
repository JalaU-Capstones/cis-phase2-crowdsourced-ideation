using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CIS_Phase2_Crowdsourced_Ideation.Features.Ideas;

public class IdeaConfiguration : IEntityTypeConfiguration<Idea>
{
    public void Configure(EntityTypeBuilder<Idea> builder)
    {
        builder.ToTable("ideas");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id")
            .HasMaxLength(36)
            .HasConversion(v => v.ToString(), v => Guid.Parse(v));

        builder.Property(i => i.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(i => i.TopicId)
            .HasColumnName("topic_id")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(i => i.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(36)
            .HasConversion(v => v.ToString(), v => Guid.Parse(v))
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne(i => i.Topic)
               .WithMany(t => t.Ideas)
               .HasForeignKey(i => i.TopicId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Votes)
               .WithOne(v => v.Idea)
               .HasForeignKey(v => v.IdeaId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
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
            .HasMaxLength(36)
            .HasConversion(v => v.ToString(), v => Guid.Parse(v))
            .IsRequired();

        builder.Property(v => v.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(36)
            .HasConversion(v => v.ToString(), v => Guid.Parse(v))
            .IsRequired();

        builder.HasIndex(v => new { v.IdeaId, v.UserId })
            .IsUnique()
            .HasDatabaseName("uq_votes_idea_user");

        builder.HasOne(v => v.Idea)
            .WithMany(i => i.Votes)
            .HasForeignKey(v => v.IdeaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
