using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Procurement;

public sealed class ProcurementWorksCloseoutActionConfiguration :
    IEntityTypeConfiguration<ProcurementWorksCloseoutAction>
{
    public void Configure(EntityTypeBuilder<ProcurementWorksCloseoutAction> builder)
    {
        builder.ToTable("ProcurementWorksCloseoutActions", table =>
            table.HasTrigger("TR_ProcurementWorksCloseoutActions_TDC0409Protected"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.ContractId, item.Sequence })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ContractId,
            item.ActionType,
            item.Status
        });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_Sequence",
            "[Sequence] >= 1");
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_Type",
            "[ActionType] >= 0 AND [ActionType] <= 10");
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_Status",
            "[Status] >= 0 AND [Status] <= 3");
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_Hashes",
            "LEN([SourceSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([SourceSnapshotJson]) = 1 AND ISJSON([ReadinessSnapshotJson]) = 1");
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_NoAutoPost",
            "[AmountAutoPosted] = 0");
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_FinanceBoundary",
            "([ActionType] IN (5,8,9) AND [RequiresIndependentFinanceApproval] = 1) OR " +
            "([ActionType] NOT IN (5,8,9) AND [RequiresIndependentFinanceApproval] = 0)");
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_Currency",
            "([Amount] IS NULL AND [Currency] IS NULL) OR ([Amount] IS NOT NULL AND [Amount] >= 0 AND LEN([Currency]) = 3)");
        builder.HasOne(item => item.Contract).WithMany()
            .HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Project).WithMany()
            .HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProjectHandoverItem).WithMany()
            .HasForeignKey(item => item.ProjectHandoverItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProjectDefectLiabilityCase).WithMany()
            .HasForeignKey(item => item.ProjectDefectLiabilityCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProjectFinalAccount).WithMany()
            .HasForeignKey(item => item.ProjectFinalAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProjectPaymentCertificate).WithMany()
            .HasForeignKey(item => item.ProjectPaymentCertificateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PerformanceBondRequest).WithMany()
            .HasForeignKey(item => item.PerformanceBondRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementWorksCloseoutEvidenceConfiguration :
    IEntityTypeConfiguration<ProcurementWorksCloseoutEvidence>
{
    public void Configure(EntityTypeBuilder<ProcurementWorksCloseoutEvidence> builder)
    {
        builder.ToTable("ProcurementWorksCloseoutEvidence", table =>
            table.HasTrigger("TR_ProcurementWorksCloseoutEvidence_TDC0409Immutable"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ActionId,
            item.RequirementKey
        }).IsUnique();
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutEvidence_Reference",
            "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)");
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutEvidence_Hash",
            "LEN([EvidenceHash]) = 64");
        builder.HasOne(item => item.Action).WithMany(item => item.Evidence)
            .HasForeignKey(item => item.ActionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
