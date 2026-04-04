using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Ideas;

public sealed class IdeaConfiguration : IEntityTypeConfiguration<Idea>
{
    public void Configure(EntityTypeBuilder<Idea> builder)
    {
        builder.ToTable("ideas");
        
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").HasMaxLength(36);
        
        builder.Property(i => i.TopicId).HasColumnName("topic_id").IsRequired().HasMaxLength(36);
        builder.HasIndex(i => i.TopicId).HasDatabaseName("ix_ideas_topic_id");
        
        builder.Property(i => i.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(i => i.Description).HasColumnName("description").HasMaxLength(2000);
        
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").IsRequired().HasMaxLength(36);
        builder.HasIndex(i => i.CreatedBy).HasDatabaseName("ix_ideas_created_by");
        
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at").IsRequired();
        
        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);
        
        builder.Property(i => i.VoteCount).HasColumnName("vote_count").HasDefaultValue(0);
        
        builder.HasIndex(i => new { i.TopicId, i.Status })
            .HasDatabaseName("ix_ideas_topic_id_status");
    }
}