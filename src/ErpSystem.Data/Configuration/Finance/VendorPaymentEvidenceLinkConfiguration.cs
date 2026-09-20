using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Finance;

public sealed class VendorPaymentEvidenceLinkConfiguration : IEntityTypeConfiguration<VendorPaymentEvidenceLink>
{
    public void Configure(EntityTypeBuilder<VendorPaymentEvidenceLink> entity)
    {
        entity.ToTable("VendorPaymentEvidenceLinks", table => table.HasTrigger("TR_VendorPaymentEvidenceLinks_SourceGuard"));
        entity.HasIndex(item => new { item.TenantId, item.ClientRequestId }).IsUnique()
            .HasDatabaseName("UX_VendorPaymentEvidenceLinks_Tenant_Request");
        entity.HasIndex(item => new { item.TenantId, item.VendorPaymentId, item.RequirementKey, item.ChecksumSha256 }).IsUnique()
            .HasDatabaseName("UX_VendorPaymentEvidenceLinks_Tenant_Payment_Requirement_Hash");
        entity.HasOne(item => item.VendorPayment).WithMany().HasForeignKey(item => item.VendorPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.FileUploadRecord).WithMany().HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.CentralDocumentRecord).WithMany().HasForeignKey(item => item.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.CentralDocumentVersion).WithMany().HasForeignKey(item => item.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
