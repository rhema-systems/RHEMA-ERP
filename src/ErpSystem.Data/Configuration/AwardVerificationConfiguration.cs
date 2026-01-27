using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class AwardVerificationChecklistTemplateConfiguration : IEntityTypeConfiguration<AwardVerificationChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<AwardVerificationChecklistTemplate> builder)
    {
        builder.ToTable("AwardVerificationChecklistTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
        builder.HasIndex(t => t.IsActive);
        builder.HasIndex(t => t.IsDefault);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Items)
            .WithOne(i => i.Template)
            .HasForeignKey(i => i.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AwardVerificationChecklistItemConfiguration : IEntityTypeConfiguration<AwardVerificationChecklistItem>
{
    public void Configure(EntityTypeBuilder<AwardVerificationChecklistItem> builder)
    {
        builder.ToTable("AwardVerificationChecklistItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemText)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(i => i.TemplateId);
        builder.HasIndex(i => i.DisplayOrder);

        builder.HasOne(i => i.Tenant)
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenderAwardVerificationConfiguration : IEntityTypeConfiguration<TenderAwardVerification>
{
    public void Configure(EntityTypeBuilder<TenderAwardVerification> builder)
    {
        builder.ToTable("TenderAwardVerifications");

        builder.HasKey(v => v.Id);

        builder.HasIndex(v => v.TenderId);
        builder.HasIndex(v => v.Status);

        builder.HasOne(v => v.Tenant)
            .WithMany()
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Tender)
            .WithMany()
            .HasForeignKey(v => v.TenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Template)
            .WithMany()
            .HasForeignKey(v => v.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.Bidders)
            .WithOne(b => b.Verification)
            .HasForeignKey(b => b.VerificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenderAwardVerificationBidderConfiguration : IEntityTypeConfiguration<TenderAwardVerificationBidder>
{
    public void Configure(EntityTypeBuilder<TenderAwardVerificationBidder> builder)
    {
        builder.ToTable("TenderAwardVerificationBidders");

        builder.HasKey(b => b.Id);

        builder.HasIndex(b => b.VerificationId);
        builder.HasIndex(b => b.TenderBidId);
        builder.HasIndex(b => b.BusinessPartnerId);

        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.TenderBid)
            .WithMany()
            .HasForeignKey(b => b.TenderBidId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.BusinessPartner)
            .WithMany()
            .HasForeignKey(b => b.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.ItemResults)
            .WithOne(r => r.Bidder)
            .HasForeignKey(r => r.BidderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenderAwardVerificationItemResultConfiguration : IEntityTypeConfiguration<TenderAwardVerificationItemResult>
{
    public void Configure(EntityTypeBuilder<TenderAwardVerificationItemResult> builder)
    {
        builder.ToTable("TenderAwardVerificationItemResults");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.BidderId);
        builder.HasIndex(r => r.ChecklistItemId);
        builder.HasIndex(r => new { r.BidderId, r.ChecklistItemId }).IsUnique();

        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ChecklistItem)
            .WithMany()
            .HasForeignKey(r => r.ChecklistItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Documents)
            .WithOne(d => d.ItemResult)
            .HasForeignKey(d => d.ItemResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenderAwardVerificationItemDocumentConfiguration : IEntityTypeConfiguration<TenderAwardVerificationItemDocument>
{
    public void Configure(EntityTypeBuilder<TenderAwardVerificationItemDocument> builder)
    {
        builder.ToTable("TenderAwardVerificationItemDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.ContentType)
            .HasMaxLength(100);

        builder.Property(d => d.DocumentType)
            .HasMaxLength(50);

        builder.Property(d => d.Description)
            .HasMaxLength(500);

        builder.HasIndex(d => d.ItemResultId);

        builder.HasOne(d => d.Tenant)
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

