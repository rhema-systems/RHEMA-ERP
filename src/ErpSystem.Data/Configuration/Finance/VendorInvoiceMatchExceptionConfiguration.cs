using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Finance;

public sealed class VendorInvoiceMatchExceptionConfiguration :
    IEntityTypeConfiguration<VendorInvoiceMatchException>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceMatchException> builder)
    {
        builder.ToTable("VendorInvoiceMatchException", table =>
            table.HasTrigger("TR_VendorInvoiceMatchException_TDC0507Protected"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.VendorInvoiceId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status, item.ExpiresAtUtc });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.CorrectiveActionStatus,
            item.CorrectiveActionDueAtUtc
        });
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507Status", "[Status] BETWEEN 1 AND 5");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507CorrectiveStatus",
            "[CorrectiveActionStatus] BETWEEN 1 AND 2");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507Sequence", "[Sequence] >= 1");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507Tolerances",
            "[PriceTolerancePercent] BETWEEN 0 AND 100 AND [QuantityTolerancePercent] BETWEEN 0 AND 100");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507Dates",
            "[ExpiresAtUtc] > [RequestedAtUtc] AND [CorrectiveActionDueAtUtc] > [RequestedAtUtc]");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507Hashes",
            "LEN([InvoiceSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([VarianceSnapshotJson]) = 1");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507Approval",
            "([Status] <> 2) OR ([WorkflowInstanceId] IS NOT NULL AND [ApprovalControlEventId] IS NOT NULL AND [FinalApprovedById] IS NOT NULL AND [FinalApprovedAtUtc] IS NOT NULL)");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchException_TDC0507CorrectiveCompletion",
            "([CorrectiveActionStatus] = 1 AND [CorrectiveActionCompletedAtUtc] IS NULL AND [CorrectiveActionCompletedById] IS NULL) OR ([CorrectiveActionStatus] = 2 AND [CorrectiveActionCompletedAtUtc] IS NOT NULL AND [CorrectiveActionCompletedById] IS NOT NULL)");
        builder.HasOne(item => item.VendorInvoice).WithMany()
            .HasForeignKey(item => item.VendorInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PurchaseOrder).WithMany()
            .HasForeignKey(item => item.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ApprovalControlEvent).WithMany()
            .HasForeignKey(item => item.ApprovalControlEventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VendorInvoiceMatchExceptionVarianceConfiguration :
    IEntityTypeConfiguration<VendorInvoiceMatchExceptionVariance>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceMatchExceptionVariance> builder)
    {
        builder.ToTable("VendorInvoiceMatchExceptionVariance", table =>
            table.HasTrigger("TR_VendorInvoiceMatchExceptionVariance_TDC0507Immutable"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.MatchExceptionId });
        builder.HasCheckConstraint("CK_VendorInvoiceMatchExceptionVariance_TDC0507Tolerance",
            "[ConfiguredTolerancePercent] BETWEEN 0 AND 100");
        builder.HasOne(item => item.MatchException).WithMany(item => item.Variances)
            .HasForeignKey(item => item.MatchExceptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VendorInvoiceMatchExceptionEvidenceConfiguration :
    IEntityTypeConfiguration<VendorInvoiceMatchExceptionEvidence>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceMatchExceptionEvidence> builder)
    {
        builder.ToTable("VendorInvoiceMatchExceptionEvidence", table =>
            table.HasTrigger("TR_VendorInvoiceMatchExceptionEvidence_TDC0507Immutable"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.MatchExceptionId, item.RequirementKey }).IsUnique();
        builder.HasCheckConstraint("CK_VendorInvoiceMatchExceptionEvidence_TDC0507Reference",
            "([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchExceptionEvidence_TDC0507Hash",
            "LEN([EvidenceHash]) = 64");
        builder.HasOne(item => item.MatchException).WithMany(item => item.Evidence)
            .HasForeignKey(item => item.MatchExceptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VendorInvoiceMatchExceptionActionConfiguration :
    IEntityTypeConfiguration<VendorInvoiceMatchExceptionAction>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceMatchExceptionAction> builder)
    {
        builder.ToTable("VendorInvoiceMatchExceptionAction", table =>
            table.HasTrigger("TR_VendorInvoiceMatchExceptionAction_TDC0507Immutable"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.MatchExceptionId, item.Sequence }).IsUnique();
        builder.HasCheckConstraint("CK_VendorInvoiceMatchExceptionAction_TDC0507Sequence", "[Sequence] >= 1");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchExceptionAction_TDC0507Statuses",
            "[FromStatus] BETWEEN 1 AND 5 AND [ToStatus] BETWEEN 1 AND 5");
        builder.HasCheckConstraint("CK_VendorInvoiceMatchExceptionAction_TDC0507Hash",
            "LEN([IntegrityHash]) = 64");
        builder.HasOne(item => item.MatchException).WithMany(item => item.Actions)
            .HasForeignKey(item => item.MatchExceptionId).OnDelete(DeleteBehavior.Restrict);
    }
}
