using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class RequestForQuotationConfiguration : IEntityTypeConfiguration<RequestForQuotation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotation> builder)
    {
        builder.ToTable("RequestForQuotations", table =>
        {
            table.HasTrigger("TR_RequestForQuotations_SourcingReleaseGuard");
            table.HasTrigger("TR_RequestForQuotations_StatutoryLifecycleGuard");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RfqNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(30);

        builder.HasIndex(r => new { r.TenantId, r.RfqNumber }).IsUnique();
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.SubmissionDeadline);
        builder.HasIndex(r => r.SourcePurchaseRequisitionId);
        builder.HasIndex(r => r.SourcingReleaseId);
        builder.HasIndex(r => r.SourcingCaseId);

        builder.HasOne(r => r.SourcePurchaseRequisition)
            .WithMany()
            .HasForeignKey(r => r.SourcePurchaseRequisitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.SourcingRelease)
            .WithMany()
            .HasForeignKey(r => r.SourcingReleaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.SourcingCase)
            .WithMany()
            .HasForeignKey(r => r.SourcingCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items)
            .WithOne(i => i.Rfq)
            .HasForeignKey(i => i.RfqId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Invitations)
            .WithOne(i => i.Rfq)
            .HasForeignKey(i => i.RfqId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Quotes)
            .WithOne(q => q.Rfq)
            .HasForeignKey(q => q.RfqId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.AwardLines)
            .WithOne(a => a.Rfq)
            .HasForeignKey(a => a.RfqId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RequestForQuotationItemConfiguration : IEntityTypeConfiguration<RequestForQuotationItem>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationItem> builder)
    {
        builder.ToTable("RequestForQuotationItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.UnitOfMeasure).HasMaxLength(50);
        builder.Property(i => i.ItemCode).HasMaxLength(50);

        builder.HasIndex(i => new { i.TenantId, i.RfqId, i.LineNumber }).IsUnique();
        builder.HasIndex(i => i.SourcePurchaseRequisitionItemId);
    }
}

public class RequestForQuotationInvitationConfiguration : IEntityTypeConfiguration<RequestForQuotationInvitation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationInvitation> builder)
    {
        builder.ToTable("RequestForQuotationInvitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Status).HasMaxLength(30);

        builder.HasIndex(i => new { i.TenantId, i.RfqId, i.BusinessPartnerId }).IsUnique();

        builder.HasOne(i => i.BusinessPartner)
            .WithMany()
            .HasForeignKey(i => i.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequestForQuotationQuoteConfiguration : IEntityTypeConfiguration<RequestForQuotationQuote>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationQuote> builder)
    {
        builder.ToTable("RequestForQuotationQuotes");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Status).HasMaxLength(20);

        builder.HasIndex(q => new { q.TenantId, q.RfqId, q.BusinessPartnerId }).IsUnique();

        builder.HasOne(q => q.BusinessPartner)
            .WithMany()
            .HasForeignKey(q => q.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Items)
            .WithOne(i => i.Quote)
            .HasForeignKey(i => i.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RequestForQuotationQuoteItemConfiguration : IEntityTypeConfiguration<RequestForQuotationQuoteItem>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationQuoteItem> builder)
    {
        builder.ToTable("RequestForQuotationQuoteItems");

        builder.HasKey(i => i.Id);

        builder.HasIndex(i => new { i.TenantId, i.QuoteId, i.RfqItemId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Avoid SQL Server "multiple cascade paths" between RFQ -> Items/Quotes -> QuoteItems.
        // We keep cascade from Quote -> QuoteItems, and restrict from RfqItem -> QuoteItems.
        builder.HasOne(i => i.RfqItem)
            .WithMany()
            .HasForeignKey(i => i.RfqItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Preserve the existing database column and snapshot relationship even though the
        // legacy quote-item CLR type does not expose UOM identity directly. Removing this
        // shadow mapping would scaffold an unintended destructive column drop.
        builder.Property<Guid?>("UnitOfMeasureId");
        builder.HasOne<ErpSystem.Core.Entities.Inventory.UnitOfMeasure>()
            .WithMany()
            .HasForeignKey("UnitOfMeasureId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequestForQuotationAwardLineConfiguration : IEntityTypeConfiguration<RequestForQuotationAwardLine>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationAwardLine> builder)
    {
        builder.ToTable("RequestForQuotationAwardLines");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UnitPrice).HasColumnType("decimal(18,4)");
        builder.Property(a => a.LineTotal).HasColumnType("decimal(18,2)");

        // One award per RFQ item (per tenant).
        builder.HasIndex(a => new { a.TenantId, a.RfqId, a.RfqItemId }).IsUnique();

        builder.HasOne(a => a.BusinessPartner)
            .WithMany()
            .HasForeignKey(a => a.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Quote)
            .WithMany()
            .HasForeignKey(a => a.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Avoid multiple cascade paths (RFQ -> Items, RFQ -> AwardLines).
        builder.HasOne(a => a.RfqItem)
            .WithMany()
            .HasForeignKey(a => a.RfqItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
