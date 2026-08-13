using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Procurement;

public sealed class ProcurementReceiptSourceEvidenceConfiguration :
    IEntityTypeConfiguration<ProcurementReceiptSourceEvidence>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptSourceEvidence> builder)
    {
        builder.ToTable("ProcurementReceiptSourceEvidence", table =>
            table.HasTrigger("TR_ProcurementReceiptSourceEvidence_INVREQFU001"));
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderReceiptId, item.EvidenceKind })
            .IsUnique().HasFilter("[IsCurrent] = 1 AND [IsDeleted] = 0");
        builder.HasCheckConstraint("CK_ProcurementReceiptSourceEvidence_Kind", "[EvidenceKind] IN (1,2)");
        builder.HasCheckConstraint("CK_ProcurementReceiptSourceEvidence_Hashes", "LEN([RequestHash]) = 64 AND LEN([ChecksumSha256]) = 64");
        builder.HasCheckConstraint("CK_ProcurementReceiptSourceEvidence_File", "[FileSize] > 0");
        builder.HasCheckConstraint("CK_ProcurementReceiptSourceEvidence_Current", "([IsCurrent] = 1 AND [SupersededAtUtc] IS NULL AND [SupersededByEvidenceId] IS NULL) OR ([IsCurrent] = 0 AND [SupersededAtUtc] IS NOT NULL AND [SupersededByEvidenceId] IS NOT NULL)");
        builder.HasOne(item => item.PurchaseOrderReceipt).WithMany()
            .HasForeignKey(item => item.PurchaseOrderReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CentralDocumentRecord).WithMany()
            .HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CentralDocumentVersion).WithMany()
            .HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
