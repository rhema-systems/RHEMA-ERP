using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class TenderConfiguration : IEntityTypeConfiguration<Tender>
{
    public void Configure(EntityTypeBuilder<Tender> builder)
    {
        builder.ToTable("Tenders", table =>
            table.HasTrigger("TR_Tenders_SourcingReleaseGuard"));

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
        builder.HasIndex(t => t.SourcePurchaseRequisitionId);
        builder.HasIndex(t => t.SourcingReleaseId);
        builder.HasIndex(t => t.SourcingCaseId);

        // Relationships
        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.SourcePurchaseRequisition)
            .WithMany()
            .HasForeignKey(t => t.SourcePurchaseRequisitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.SourcingRelease)
            .WithMany()
            .HasForeignKey(t => t.SourcingReleaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.SourcingCase)
            .WithMany()
            .HasForeignKey(t => t.SourcingCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Lots)
            .WithOne(l => l.Tender)
            .HasForeignKey(l => l.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

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

        builder.HasMany(b => b.BidLots)
            .WithOne(bl => bl.TenderBid)
            .HasForeignKey(bl => bl.TenderBidId)
            .OnDelete(DeleteBehavior.Cascade);

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

public sealed class TenderFeeConfiguration : IEntityTypeConfiguration<TenderFee>
{
    public void Configure(EntityTypeBuilder<TenderFee> builder)
    {
        builder.HasIndex(fee => fee.ReceivingAccountId);
        builder.HasIndex(fee => fee.RevenueAccountId);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(fee => fee.ReceivingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(fee => fee.RevenueAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TenderPaymentConfiguration : IEntityTypeConfiguration<TenderPayment>
{
    public void Configure(EntityTypeBuilder<TenderPayment> builder)
    {
        builder.HasIndex(payment => payment.PostingEventId)
            .IsUnique()
            .HasFilter("[PostingEventId] IS NOT NULL");
        builder.HasIndex(payment => payment.JournalEntryId)
            .IsUnique()
            .HasFilter("[JournalEntryId] IS NOT NULL");

        builder.HasOne<FinancePostingEvent>()
            .WithMany()
            .HasForeignKey(payment => payment.PostingEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(payment => payment.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
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
        builder.HasIndex(a => a.LotId);
        builder.HasIndex(a => a.BidLotId);
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

        builder.HasOne(a => a.Lot)
            .WithMany(l => l.Awards)
            .HasForeignKey(a => a.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.BidLot)
            .WithMany()
            .HasForeignKey(a => a.BidLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.TenderBid)
            .WithMany()
            .HasForeignKey(a => a.TenderBidId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.BusinessPartner)
            .WithMany()
            .HasForeignKey(a => a.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Negotiation relationship
        builder.HasOne(a => a.Negotiation)
            .WithMany()
            .HasForeignKey(a => a.NegotiationId)
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
            .WithMany(ec => ec.TemplateCriteria)
            .HasForeignKey(tc => tc.EvaluationCriterionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenderLotConfiguration : IEntityTypeConfiguration<TenderLot>
{
    public void Configure(EntityTypeBuilder<TenderLot> builder)
    {
        builder.ToTable("TenderLots");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.LotCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(l => new { l.TenderId, l.LotCode }).IsUnique();
        builder.HasIndex(l => l.TenderId);
        builder.HasIndex(l => l.Status);

        // Relationships
        builder.HasOne(l => l.Tenant)
            .WithMany()
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Tender)
            .WithMany(t => t.Lots)
            .HasForeignKey(l => l.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(l => l.Items)
            .WithOne(i => i.Lot)
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.BidLots)
            .WithOne(bl => bl.Lot)
            .HasForeignKey(bl => bl.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Awards)
            .WithOne(a => a.Lot)
            .HasForeignKey(a => a.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenderBidLotConfiguration : IEntityTypeConfiguration<TenderBidLot>
{
    public void Configure(EntityTypeBuilder<TenderBidLot> builder)
    {
        builder.ToTable("TenderBidLots");

        builder.HasKey(bl => bl.Id);

        builder.HasIndex(bl => new { bl.TenderBidId, bl.LotId }).IsUnique();
        builder.HasIndex(bl => bl.TenderBidId);
        builder.HasIndex(bl => bl.LotId);
        builder.HasIndex(bl => bl.Status);

        // Relationships
        builder.HasOne(bl => bl.Tenant)
            .WithMany()
            .HasForeignKey(bl => bl.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(bl => bl.TenderBid)
            .WithMany(b => b.BidLots)
            .HasForeignKey(bl => bl.TenderBidId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bl => bl.Lot)
            .WithMany(l => l.BidLots)
            .HasForeignKey(bl => bl.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(bl => bl.Items)
            .WithOne(i => i.BidLot)
            .HasForeignKey(i => i.BidLotId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}

public class TenderBidItemConfiguration : IEntityTypeConfiguration<TenderBidItem>
{
    public void Configure(EntityTypeBuilder<TenderBidItem> builder)
    {
        builder.ToTable("TenderBidItems");

        builder.HasKey(i => i.Id);

        builder.HasIndex(i => i.TenderBidId);
        builder.HasIndex(i => i.BidLotId);
        builder.HasIndex(i => i.TenderItemId);

        builder.HasOne(i => i.Tenant)
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.TenderBid)
            .WithMany(b => b.Items)
            .HasForeignKey(i => i.TenderBidId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.BidLot)
            .WithMany(bl => bl.Items)
            .HasForeignKey(i => i.BidLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.TenderItem)
            .WithMany(ti => ti.BidItems)
            .HasForeignKey(i => i.TenderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenderNegotiationConfiguration : IEntityTypeConfiguration<TenderNegotiation>
{
    public void Configure(EntityTypeBuilder<TenderNegotiation> builder)
    {
        builder.ToTable("TenderNegotiations");

        builder.HasKey(n => n.Id);

        builder.HasIndex(n => n.TenderId);
        builder.HasIndex(n => n.TenderBidId);
        builder.HasIndex(n => n.BusinessPartnerId);
        builder.HasIndex(n => n.LotId);
        builder.HasIndex(n => n.BidLotId);
        builder.HasIndex(n => n.Status);

        builder.HasOne(n => n.Tenant)
            .WithMany()
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Tender)
            .WithMany()
            .HasForeignKey(n => n.TenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.TenderBid)
            .WithMany()
            .HasForeignKey(n => n.TenderBidId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.BusinessPartner)
            .WithMany()
            .HasForeignKey(n => n.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Lot)
            .WithMany()
            .HasForeignKey(n => n.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.BidLot)
            .WithMany()
            .HasForeignKey(n => n.BidLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.InvitedBy)
            .WithMany()
            .HasForeignKey(n => n.InvitedById)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(n => n.CompletedBy)
            .WithMany()
            .HasForeignKey(n => n.CompletedById)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(n => n.Items)
            .WithOne(i => i.Negotiation)
            .HasForeignKey(i => i.NegotiationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenderNegotiationItemConfiguration : IEntityTypeConfiguration<TenderNegotiationItem>
{
    public void Configure(EntityTypeBuilder<TenderNegotiationItem> builder)
    {
        builder.ToTable("TenderNegotiationItems");

        builder.HasKey(i => i.Id);

        builder.HasIndex(i => i.NegotiationId);
        builder.HasIndex(i => i.TenderBidItemId);

        builder.HasOne(i => i.Tenant)
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Negotiation)
            .WithMany(n => n.Items)
            .HasForeignKey(i => i.NegotiationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.TenderBidItem)
            .WithMany()
            .HasForeignKey(i => i.TenderBidItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
