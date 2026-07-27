using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementSupplierDueDiligenceReviewConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierDueDiligenceReview>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierDueDiligenceReview> builder)
    {
        builder.ToTable("ProcurementSupplierDueDiligenceReviews", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierDueDiligenceReviews_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierDueDiligenceReviews_State",
                "[CycleNumber] >= 1 AND [ReviewType] BETWEEN 0 AND 1 " +
                "AND [Status] BETWEEN 0 AND 5 AND [Outcome] BETWEEN 0 AND 2 " +
                "AND [ReviewPeriodEndUtc] > [ReviewPeriodStartUtc] " +
                "AND [ReviewFrequencyMonths] BETWEEN 1 AND 120 " +
                "AND [PolicyProfileVersion] >= 1 AND LEN([PolicyValueHash]) = 64 " +
                "AND LEN([CreationCorrelationId]) > 0 " +
                "AND LEN([LastOperationCorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([PolicySnapshotJson]) = 1 AND ISJSON([SnapshotJson]) = 1 " +
                "AND (([Status] = 0 AND [Outcome] = 0) OR [Status] <> 0) " +
                "AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL) " +
                "OR [Status] <> 1) " +
                "AND (([Status] = 2 AND [ApprovedById] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL) " +
                "OR [Status] <> 2) " +
                "AND (([Status] = 3 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) " +
                "OR [Status] <> 3) " +
                "AND (([Status] = 4 AND [ExpiredAtUtc] IS NOT NULL) OR [Status] <> 4) " +
                "AND (([Status] = 5 AND [SupersededByReviewId] IS NOT NULL) OR [Status] <> 5)");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.TenantId, item.ReviewReference }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.CycleNumber })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId })
            .IsUnique().HasFilter("[Status] IN (0, 1) AND [IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.Status })
            .IsUnique().HasFilter("[Status] = 2 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_ProcurementSupplierDueDiligenceReviews_CurrentApproved");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.ReviewPeriodEndUtc });
        builder.HasIndex(item => new { item.TenantId, item.PolicyDecisionId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicyDecision).WithMany()
            .HasForeignKey(item => item.PolicyDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicyProfile).WithMany()
            .HasForeignKey(item => item.PolicyProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SupersedesReview).WithMany()
            .HasForeignKey(item => item.SupersedesReviewId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierDueDiligenceCheckConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierDueDiligenceCheck>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierDueDiligenceCheck> builder)
    {
        builder.ToTable("ProcurementSupplierDueDiligenceChecks", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierDueDiligenceChecks_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierDueDiligenceChecks_State",
                "[CheckType] BETWEEN 0 AND 5 AND [Status] BETWEEN 0 AND 3 " +
                "AND LEN([SourceName]) > 0 AND LEN([SourceReference]) > 0 " +
                "AND ([ValidUntilUtc] IS NULL OR [CheckedAtUtc] IS NULL " +
                "OR [ValidUntilUtc] > [CheckedAtUtc]) " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.ReviewId, item.CheckType })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status, item.ValidUntilUtc });
        builder.HasOne(item => item.Review).WithMany(item => item.Checks)
            .HasForeignKey(item => item.ReviewId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierDueDiligenceEvidenceLinkConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierDueDiligenceEvidenceLink>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierDueDiligenceEvidenceLink> builder)
    {
        builder.ToTable("ProcurementSupplierDueDiligenceEvidenceLinks", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierDueDiligenceEvidenceLinks_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierDueDiligenceEvidenceLinks_State",
                "[ReferenceKind] BETWEEN 0 AND 2 AND LEN([Reference]) > 0 " +
                "AND LEN([RequirementKey]) > 0 AND LEN([IntegrityHash]) = 64 " +
                "AND (([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL " +
                "AND [FileUploadRecordId] IS NULL) " +
                "OR ([ReferenceKind] = 1 AND [FileUploadRecordId] IS NOT NULL " +
                "AND [WorkflowEvidenceDocumentId] IS NULL) " +
                "OR ([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL " +
                "AND [FileUploadRecordId] IS NULL))");
        });

        builder.HasIndex(item => new
        {
            item.TenantId,
            item.CheckId,
            item.ReferenceKind,
            item.Reference
        }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ReviewId });

        builder.HasOne(item => item.Review).WithMany()
            .HasForeignKey(item => item.ReviewId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Check).WithMany(item => item.EvidenceLinks)
            .HasForeignKey(item => item.CheckId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
