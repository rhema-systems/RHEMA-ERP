using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Procurement;

public sealed class ProcurementReceiptDocumentConfiguration : IEntityTypeConfiguration<ProcurementReceiptDocument>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptDocument> builder)
    {
        builder.ToTable("ProcurementReceiptDocuments", table =>
            table.HasTrigger("TR_ProcurementReceiptDocuments_TDC0509Protected"));
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderReceiptId, item.DocumentKind })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.DocumentNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status, item.ReconciliationStatus });
        builder.HasCheckConstraint("CK_ProcurementReceiptDocuments_Kind", "[DocumentKind] BETWEEN 0 AND 1");
        builder.HasCheckConstraint("CK_ProcurementReceiptDocuments_Status", "[Status] BETWEEN 0 AND 3 AND [ReconciliationStatus] BETWEEN 0 AND 3");
        builder.HasCheckConstraint("CK_ProcurementReceiptDocuments_Snapshots", "ISJSON([DecisionKeysJson]) = 1 AND ISJSON([DecisionSnapshotJson]) = 1 AND ISJSON([SourceSnapshotJson]) = 1 AND LEN([SourceIntegrityHash]) = 64");
        builder.HasOne(item => item.PurchaseOrderReceipt).WithMany()
            .HasForeignKey(item => item.PurchaseOrderReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.GoodsReceiptNote).WithMany()
            .HasForeignKey(item => item.GoodsReceiptNoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CentralDocumentRecord).WithMany()
            .HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CentralDocumentVersion).WithMany()
            .HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementConfigurationProfile>().WithMany()
            .HasForeignKey(item => item.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementConfigurationDecision>().WithMany()
            .HasForeignKey(item => item.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementReceiptDocumentSignatureConfiguration : IEntityTypeConfiguration<ProcurementReceiptDocumentSignature>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptDocumentSignature> builder)
    {
        builder.ToTable("ProcurementReceiptDocumentSignatures", table =>
            table.HasTrigger("TR_ProcurementReceiptDocumentSignatures_TDC0509Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.ReceiptDocumentId, item.RequiredRole }).IsUnique();
        builder.HasCheckConstraint("CK_ProcurementReceiptDocumentSignatures_Hash", "LEN([IntegrityHash]) = 64");
        builder.HasOne(item => item.ReceiptDocument).WithMany(item => item.Signatures)
            .HasForeignKey(item => item.ReceiptDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementReceiptDocumentActionConfiguration : IEntityTypeConfiguration<ProcurementReceiptDocumentAction>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptDocumentAction> builder)
    {
        builder.ToTable("ProcurementReceiptDocumentActions", table =>
            table.HasTrigger("TR_ProcurementReceiptDocumentActions_TDC0509Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.ReceiptDocumentId, item.OccurredAtUtc });
        builder.HasCheckConstraint("CK_ProcurementReceiptDocumentActions_Details", "ISJSON([DetailsJson]) = 1 AND LEN([IntegrityHash]) = 64");
        builder.HasOne(item => item.ReceiptDocument).WithMany(item => item.Actions)
            .HasForeignKey(item => item.ReceiptDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
