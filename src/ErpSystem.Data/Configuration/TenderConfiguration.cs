using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class TenderConfiguration : IEntityTypeConfiguration<Tender>
{
    public void Configure(EntityTypeBuilder<Tender> builder)
    {
        builder.ToTable("Tenders");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(t => new { t.TenantId, t.TenderNumber }).IsUnique();
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.PublishDate);
        builder.HasIndex(t => t.SubmissionDeadline);

        // Relationships
        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Items)
            .WithOne(i => i.Tender)
            .HasForeignKey(i => i.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Bids)
            .WithOne(b => b.Tender)
            .HasForeignKey(b => b.TenderId)
            .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade to avoid cycles

        builder.HasMany(t => t.Documents)
            .WithOne(d => d.Tender)
            .HasForeignKey(d => d.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Invitations)
            .WithOne(i => i.Tender)
            .HasForeignKey(i => i.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Awards)
            .WithOne(a => a.Tender)
            .HasForeignKey(a => a.TenderId)
            .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade to avoid cycles

        builder.HasMany(t => t.Fees)
            .WithOne(f => f.Tender)
            .HasForeignKey(f => f.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Evaluators)
            .WithOne(e => e.Tender)
            .HasForeignKey(e => e.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Interviews)
            .WithOne(i => i.Tender)
            .HasForeignKey(i => i.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Clarifications)
            .WithOne(c => c.Tender)
            .HasForeignKey(c => c.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Revisions)
            .WithOne(r => r.Tender)
            .HasForeignKey(r => r.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.ViewLogs)
            .WithOne(v => v.Tender)
            .HasForeignKey(v => v.TenderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenderBidConfiguration : IEntityTypeConfiguration<TenderBid>
{
    public void Configure(EntityTypeBuilder<TenderBid> builder)
    {
        builder.ToTable("TenderBids");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BidNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(b => new { b.TenantId, b.BidNumber }).IsUnique();
        builder.HasIndex(b => b.TenderId);
        builder.HasIndex(b => b.BusinessPartnerId);
        builder.HasIndex(b => b.Status);

        // Relationships
        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Tender)
            .WithMany(t => t.Bids)
            .HasForeignKey(b => b.TenderId)
            .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade to avoid cycles

        builder.HasOne(b => b.BusinessPartner)
            .WithMany()
            .HasForeignKey(b => b.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade to avoid cycles

        builder.HasMany(b => b.Items)
            .WithOne(i => i.TenderBid)
            .HasForeignKey(i => i.TenderBidId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Documents)
            .WithOne(d => d.TenderBid)
            .HasForeignKey(d => d.TenderBidId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Evaluations)
            .WithOne(e => e.TenderBid)
            .HasForeignKey(e => e.TenderBidId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Interviews)
            .WithOne(i => i.TenderBid)
            .HasForeignKey(i => i.TenderBidId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TenderAwardConfiguration : IEntityTypeConfiguration<TenderAward>
{
    public void Configure(EntityTypeBuilder<TenderAward> builder)
    {
        builder.ToTable("TenderAwards");

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.TenderId);
        builder.HasIndex(a => a.TenderBidId);
        builder.HasIndex(a => a.BusinessPartnerId);
        builder.HasIndex(a => a.Status);

        // Relationships - All set to Restrict to avoid cascade cycles
        builder.HasOne(a => a.Tenant)
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Tender)
            .WithMany(t => t.Awards)
            .HasForeignKey(a => a.TenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.TenderBid)
            .WithMany()
            .HasForeignKey(a => a.TenderBidId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.BusinessPartner)
            .WithMany()
            .HasForeignKey(a => a.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class EvaluationTemplateConfiguration : IEntityTypeConfiguration<EvaluationTemplate>
{
    public void Configure(EntityTypeBuilder<EvaluationTemplate> builder)
    {
        builder.ToTable("EvaluationTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TemplateName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.Category)
            .HasMaxLength(100);

        builder.Property(t => t.TenderType)
            .HasMaxLength(50);

        builder.Property(t => t.ScoringMethod)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => new { t.TenantId, t.TemplateName }).IsUnique();
        builder.HasIndex(t => t.IsActive);
        builder.HasIndex(t => t.IsDefault);

        // Relationships
        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.TemplateCriteria)
            .WithOne(tc => tc.EvaluationTemplate)
            .HasForeignKey(tc => tc.EvaluationTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Tenders)
            .WithOne(tender => tender.EvaluationTemplate)
            .HasForeignKey(tender => tender.EvaluationTemplateId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class EvaluationTemplateCriterionConfiguration : IEntityTypeConfiguration<EvaluationTemplateCriterion>
{
    public void Configure(EntityTypeBuilder<EvaluationTemplateCriterion> builder)
    {
        builder.ToTable("EvaluationTemplateCriteria");

        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.Weight)
            .HasPrecision(5, 2);

        builder.HasIndex(tc => new { tc.EvaluationTemplateId, tc.EvaluationCriterionId }).IsUnique();
        builder.HasIndex(tc => tc.DisplayOrder);

        // Relationships
        builder.HasOne(tc => tc.Tenant)
            .WithMany()
            .HasForeignKey(tc => tc.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tc => tc.EvaluationTemplate)
            .WithMany(t => t.TemplateCriteria)
            .HasForeignKey(tc => tc.EvaluationTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tc => tc.EvaluationCriterion)
            .WithMany()
            .HasForeignKey(tc => tc.EvaluationCriterionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

