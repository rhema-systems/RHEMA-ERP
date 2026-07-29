using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementSupplierRiskAssessmentConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierRiskAssessment>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierRiskAssessment> builder)
    {
        builder.ToTable("ProcurementSupplierRiskAssessments", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierRiskAssessments_AppendOnly");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierRiskAssessments_State",
                "[AssessmentSequence] >= 1 AND [PeriodEndUtc] >= [PeriodStartUtc] " +
                "AND [NextReviewDueAtUtc] > [AssessedAtUtc] AND [ExposureWindowMonths] BETWEEN 1 AND 120 " +
                "AND [MinimumScore] BETWEEN 0 AND 100 AND [ConcentrationLimitPercent] BETWEEN 0 AND 100 " +
                "AND ([RiskScore] IS NULL OR [RiskScore] BETWEEN 0 AND 100) " +
                "AND [MaximumSpendSharePercent] BETWEEN 0 AND 100 AND [SingleSourceCategoryCount] >= 0 " +
                "AND [EligibilityAction] BETWEEN 0 AND 2 AND [PolicyProfileVersion] >= 1 " +
                "AND LEN([PolicyValueHash]) = 64 AND LEN([EligibilityDecisionHash]) = 64 " +
                "AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 " +
                "AND ISJSON([DimensionScoresJson]) = 1 AND ISJSON([SpendExposureJson]) = 1 " +
                "AND ISJSON([CategoryExposureJson]) = 1 AND ISJSON([EligibilitySnapshotJson]) = 1 " +
                "AND ISJSON([FindingsJson]) = 1 AND ISJSON([SnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.AssessmentReference }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.AssessmentSequence })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.AssessedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.PolicyDecisionId });
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicyDecision).WithMany()
            .HasForeignKey(item => item.PolicyDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicyProfile).WithMany()
            .HasForeignKey(item => item.PolicyProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierRiskAlertConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierRiskAlert>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierRiskAlert> builder)
    {
        builder.ToTable("ProcurementSupplierRiskAlerts", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierRiskAlerts_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierRiskAlerts_State",
                "[AlertType] BETWEEN 0 AND 3 AND [Status] BETWEEN 0 AND 2 " +
                "AND LEN([RuleCode]) > 0 AND LEN([Severity]) > 0 AND LEN([Message]) > 0 " +
                "AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 " +
                "AND ([EscalationEvidenceJson] IS NULL OR ISJSON([EscalationEvidenceJson]) = 1) " +
                "AND ([ResolutionEvidenceJson] IS NULL OR ISJSON([ResolutionEvidenceJson]) = 1) " +
                "AND (([Status] IN (1,2) AND [WorkflowDefinitionId] IS NOT NULL " +
                "AND [WorkflowInstanceId] IS NOT NULL AND [EscalatedById] IS NOT NULL " +
                "AND [EscalatedAtUtc] IS NOT NULL AND LEN([EscalationReason]) > 0) OR [Status] = 0) " +
                "AND (([Status] = 2 AND [ResolvedById] IS NOT NULL AND [ResolvedAtUtc] IS NOT NULL " +
                "AND LEN([ResolutionReason]) > 0) OR [Status] <> 2)");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.AssessmentId, item.AlertType }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });
        builder.HasOne(item => item.Assessment).WithMany(item => item.Alerts)
            .HasForeignKey(item => item.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}
