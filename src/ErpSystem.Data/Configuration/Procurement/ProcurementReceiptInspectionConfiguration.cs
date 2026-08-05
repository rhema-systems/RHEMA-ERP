using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Procurement;

public sealed class ProcurementReceiptInspectionCaseConfiguration :
    IEntityTypeConfiguration<ProcurementReceiptInspectionCase>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptInspectionCase> builder)
    {
        builder.ToTable("ProcurementReceiptInspectionCases", table =>
        {
            table.HasTrigger("TR_ProcurementReceiptInspectionCases_TDC0502Protected");
            table.HasTrigger("TR_TDC0502_ReceiptReplacementLineage");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderReceiptId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderReceiptId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.ReplacementPurchaseOrderReceiptId })
            .IsUnique()
            .HasFilter("[ReplacementPurchaseOrderReceiptId] IS NOT NULL");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionCases_Quantities",
            "[ReceivedQuantity] > 0 AND [AcceptedQuantity] >= 0 AND [RejectedQuantity] >= 0 AND [PendingQuantity] >= 0 AND [AcceptedQuantity] + [RejectedQuantity] + [PendingQuantity] = [ReceivedQuantity] AND [StockEligibleQuantity] >= 0 AND [StockPostedQuantity] >= 0 AND [StockPostedQuantity] <= [StockEligibleQuantity] AND [ApEligibleQuantity] >= 0 AND [ApBlockedQuantity] >= 0");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionCases_Status", "[Status] BETWEEN 0 AND 10");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionCases_Hashes",
            "LEN([SourceSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([SourceSnapshotJson]) = 1 AND ISJSON([DecisionSnapshotJson]) = 1");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionCases_ReplacementLineage",
            "([ReplacementPurchaseOrderReceiptId] IS NULL AND [ReplacementInspectionCaseId] IS NULL AND [ReplacementLinkedAtUtc] IS NULL) OR ([ReplacementPurchaseOrderReceiptId] IS NOT NULL AND [ReplacementInspectionCaseId] IS NOT NULL AND [ReplacementLinkedAtUtc] IS NOT NULL)");
        builder.HasOne(item => item.PurchaseOrderReceipt).WithMany()
            .HasForeignKey(item => item.PurchaseOrderReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ReplacementPurchaseOrderReceipt).WithMany()
            .HasForeignKey(item => item.ReplacementPurchaseOrderReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ReplacementInspectionCase).WithMany()
            .HasForeignKey(item => item.ReplacementInspectionCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementReceiptInspectionLineConfiguration :
    IEntityTypeConfiguration<ProcurementReceiptInspectionLine>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptInspectionLine> builder)
    {
        builder.ToTable("ProcurementReceiptInspectionLines", table =>
            table.HasTrigger("TR_ProcurementReceiptInspectionLines_TDC0502Protected"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.InspectionCaseId, item.PurchaseOrderReceiptItemId }).IsUnique();
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionLines_Quantities",
            "[ReceivedQuantity] > 0 AND [AcceptedQuantity] >= 0 AND [RejectedQuantity] >= 0 AND [PendingQuantity] >= 0 AND [AcceptedQuantity] + [RejectedQuantity] + [PendingQuantity] = [ReceivedQuantity]");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionLines_Disposition", "[Disposition] BETWEEN 0 AND 3");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionLines_Hash", "LEN([IntegrityHash]) = 64");
        builder.HasOne(item => item.InspectionCase).WithMany(item => item.Lines)
            .HasForeignKey(item => item.InspectionCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PurchaseOrderReceiptItem).WithMany()
            .HasForeignKey(item => item.PurchaseOrderReceiptItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementReceiptInspectionEvidenceConfiguration :
    IEntityTypeConfiguration<ProcurementReceiptInspectionEvidence>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptInspectionEvidence> builder)
    {
        builder.ToTable("ProcurementReceiptInspectionEvidence", table =>
            table.HasTrigger("TR_ProcurementReceiptInspectionEvidence_TDC0502Immutable"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.InspectionCaseId, item.ActionKey, item.RequirementKey }).IsUnique();
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionEvidence_Reference",
            "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionEvidence_Hash", "LEN([EvidenceHash]) = 64");
        builder.HasOne(item => item.InspectionCase).WithMany(item => item.Evidence)
            .HasForeignKey(item => item.InspectionCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementReceiptInspectionActionConfiguration :
    IEntityTypeConfiguration<ProcurementReceiptInspectionAction>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptInspectionAction> builder)
    {
        builder.ToTable("ProcurementReceiptInspectionActions", table =>
            table.HasTrigger("TR_ProcurementReceiptInspectionActions_TDC0502Immutable"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.InspectionCaseId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionActions_Sequence", "[Sequence] >= 1");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionActions_Type", "[ActionType] BETWEEN 0 AND 14");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionActions_Hash", "LEN([IntegrityHash]) = 64");
        builder.HasCheckConstraint("CK_ProcurementReceiptInspectionActions_RequestFingerprint",
            "[RequestFingerprint] IS NULL OR LEN([RequestFingerprint]) = 64");
        builder.HasOne(item => item.InspectionCase).WithMany(item => item.Actions)
            .HasForeignKey(item => item.InspectionCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
