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
        {
            table.HasTrigger("TR_ProcurementWorksCloseoutActions_TDC0409Protected");
            table.HasTrigger("TR_ProcurementWorksCloseoutActions_QS0504RetentionGuard");
        });
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
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ContractId,
            item.RetentionReleaseStage,
            item.Status
        });
        builder.HasIndex(item => new
        {
            item.TenantId,
            item.ContractId,
            item.RetentionReleaseStage,
            item.ProjectHandoverItemId
        })
            .IsUnique()
            .HasFilter("[ActionType] = 5 AND [Status] = 1 AND [ProjectHandoverItemId] IS NOT NULL AND [IsDeleted] = 0");
        builder.Property(item => item.RequestHash).IsUnicode(false);
        builder.Property(item => item.QuantitySurveyRetentionPolicyHash).IsUnicode(false);
        builder.Property(item => item.RetentionHeldSnapshot).HasColumnType("decimal(18,2)");
        builder.Property(item => item.RetentionReleasedBefore).HasColumnType("decimal(18,2)");
        builder.Property(item => item.RetentionStageLimitAmount).HasColumnType("decimal(18,2)");
        builder.Property(item => item.RetentionReleasedAfter).HasColumnType("decimal(18,2)");
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
        builder.HasCheckConstraint("CK_ProcurementWorksCloseoutActions_QsRetention",
            "[RequestHash] IS NULL OR (LEN([RequestHash]) = 64 AND (([ActionType] = 5 AND [RetentionReleaseStage] BETWEEN 0 AND 3 AND [QuantitySurveyConfigurationProfileId] IS NOT NULL AND [QuantitySurveyConfigurationProfileVersion] > 0 AND [QuantitySurveyRetentionDecisionId] IS NOT NULL AND LEN([QuantitySurveyRetentionPolicyHash]) = 64 AND [Amount] > 0 AND [RetentionHeldSnapshot] >= 0 AND [RetentionReleasedBefore] >= 0 AND [RetentionStageLimitAmount] >= [Amount] AND ABS([RetentionReleasedAfter] - ([RetentionReleasedBefore] + [Amount])) <= 0.01 AND [RetentionReleasedAfter] <= [RetentionHeldSnapshot]) OR ([ActionType] <> 5 AND [RetentionReleaseStage] IS NULL AND [QuantitySurveyConfigurationProfileId] IS NULL AND [QuantitySurveyConfigurationProfileVersion] IS NULL AND [QuantitySurveyRetentionDecisionId] IS NULL AND [QuantitySurveyRetentionPolicyHash] IS NULL AND [RetentionHeldSnapshot] IS NULL AND [RetentionReleasedBefore] IS NULL AND [RetentionStageLimitAmount] IS NULL AND [RetentionReleasedAfter] IS NULL AND [UsesRetentionBond] = 0)))");
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
        builder.HasOne<ErpSystem.Core.Entities.QuantitySurvey.QuantitySurveyConfigurationProfile>()
            .WithMany()
            .HasForeignKey(item => item.QuantitySurveyConfigurationProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.QuantitySurvey.QuantitySurveyConfigurationDecision>()
            .WithMany()
            .HasForeignKey(item => item.QuantitySurveyRetentionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
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
