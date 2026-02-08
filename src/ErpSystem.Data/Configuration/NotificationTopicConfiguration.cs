using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class NotificationTopicConfiguration : IEntityTypeConfiguration<NotificationTopic>
{
    public void Configure(EntityTypeBuilder<NotificationTopic> builder)
    {
        builder.ToTable("NotificationTopics");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Key).IsRequired().HasMaxLength(120);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.EntityType).HasMaxLength(100);
        builder.Property(t => t.IsSystem).HasDefaultValue(false);
        builder.Property(t => t.IsRequired).HasDefaultValue(false);
        builder.Property(t => t.InAppTitleTemplate).HasMaxLength(200);
        builder.Property(t => t.ActionUrlTemplate).HasMaxLength(500);

        builder.HasIndex(t => new { t.TenantId, t.Key }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(t => new { t.TenantId, t.EntityType });

        builder.HasOne(t => t.EmailTemplate)
            .WithMany()
            .HasForeignKey(t => t.EmailTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Recipients)
            .WithOne(r => r.Topic)
            .HasForeignKey(r => r.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationTopicRecipientConfiguration : IEntityTypeConfiguration<NotificationTopicRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationTopicRecipient> builder)
    {
        builder.ToTable("NotificationTopicRecipients");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RecipientKind).IsRequired().HasMaxLength(40);
        builder.Property(r => r.RecipientValue).IsRequired().HasMaxLength(200);
        builder.Property(r => r.IsSystem).HasDefaultValue(false);

        builder.HasIndex(r => new { r.TenantId, r.TopicId });
    }
}

