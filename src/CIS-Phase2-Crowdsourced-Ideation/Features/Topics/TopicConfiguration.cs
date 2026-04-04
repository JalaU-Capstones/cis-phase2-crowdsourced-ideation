using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CIS.Phase2.CrowdsourcedIdeation.Features.Topics;

/// <summary>
/// Configures the database schema for the <see cref="Topic"/> entity.
/// </summary>
public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    /// <summary>
    /// Configures the entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topics");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasMaxLength(36); // GUIDs are typically 36 chars with hyphens

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>() // Store enum as string
            .IsRequired();

        builder.Property(t => t.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(36) // GUIDs are typically 36 chars with hyphens
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}