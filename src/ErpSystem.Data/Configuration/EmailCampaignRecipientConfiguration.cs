using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class EmailCampaignRecipientConfiguration : IEntityTypeConfiguration<EmailCampaignRecipient>
{
    public void Configure(EntityTypeBuilder<EmailCampaignRecipient> builder)
    {
        builder.ToTable("EmailCampaignRecipients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceRole).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.EmailCampaignId, x.Email }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.EmailCampaignId, x.Status });
    }
}

