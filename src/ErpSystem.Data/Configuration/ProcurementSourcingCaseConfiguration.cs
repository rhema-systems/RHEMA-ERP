using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementSourcingCaseConfiguration : IEntityTypeConfiguration<ProcurementSourcingCase>
{
    public void Configure(EntityTypeBuilder<ProcurementSourcingCase> builder)
    {
        builder.ToTable("ProcurementSourcingCases", table =>
        {
            table.HasTrigger("TR_ProcurementSourcingCases_Lifecycle");
            table.HasCheckConstraint("CK_ProcurementSourcingCases_MethodSelection",
                "[RecommendedMethod] BETWEEN 0 AND 8 AND [SelectedMethod] BETWEEN 0 AND 8 AND [MethodSelectionBasis] IN (0, 1) AND " +
                "(([MethodSelectionBasis] = 0 AND [SelectedMethod] = [RecommendedMethod] AND [MethodOverrideWorkflowInstanceId] IS NULL AND [MethodOverrideReason] IS NULL AND [MethodOverrideApprovalActorsJson] IS NULL AND [MethodOverrideApprovedAtUtc] IS NULL) OR " +
                "([MethodSelectionBasis] = 1 AND [SelectedMethod] <> [RecommendedMethod] AND [ApprovedExceptionRuleId] IS NOT NULL AND [MethodOverrideWorkflowInstanceId] IS NOT NULL AND LEN(LTRIM(RTRIM([MethodOverrideReason]))) >= 5 AND ISJSON([MethodOverrideApprovalActorsJson]) = 1 AND [MethodOverrideApprovalActorsJson] <> '[]' AND [MethodOverrideApprovedAtUtc] IS NOT NULL AND [ExceptionApprovalReference] IS NOT NULL AND [ExceptionEvidenceReference] IS NOT NULL))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CaseNumber).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired().IsUnicode(false);
        builder.Property(item => item.PolicyCode).HasMaxLength(50).IsRequired().IsUnicode(false);
        builder.Property(item => item.MethodRuleCode).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(item => item.ThresholdRuleCode).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(item => item.MethodOverrideReason).HasMaxLength(1000);
        builder.Property(item => item.SourceControlFingerprint).HasMaxLength(64).IsRequired().IsFixedLength().IsUnicode(false);
        builder.Property(item => item.CaseFingerprint).HasMaxLength(64).IsRequired().IsFixedLength().IsUnicode(false);
        builder.Property(item => item.IntegrityHash).HasMaxLength(64).IsRequired().IsFixedLength().IsUnicode(false);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.CaseNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseRequisitionId, item.CaseSequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SourcingReleaseId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status });
        builder.HasOne(item => item.PurchaseRequisition).WithMany(item => item.SourcingCases)
            .HasForeignKey(item => item.PurchaseRequisitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourcingRelease).WithMany(item => item.SourcingCases)
            .HasForeignKey(item => item.SourcingReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourcePlan).WithMany().HasForeignKey(item => item.SourcePlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourcePlanItem).WithMany().HasForeignKey(item => item.SourcePlanItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicySet).WithMany().HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MethodRule).WithMany().HasForeignKey(item => item.MethodRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ThresholdRule).WithMany().HasForeignKey(item => item.ThresholdRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AuthorityRoute).WithMany().HasForeignKey(item => item.AuthorityRouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ApprovedExceptionRule).WithMany().HasForeignKey(item => item.ApprovedExceptionRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MethodOverrideWorkflowInstance).WithMany()
            .HasForeignKey(item => item.MethodOverrideWorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSourcingCaseLotConfiguration : IEntityTypeConfiguration<ProcurementSourcingCaseLot>
{
    public void Configure(EntityTypeBuilder<ProcurementSourcingCaseLot> builder)
    {
        builder.ToTable("ProcurementSourcingCaseLots", table => table.HasTrigger("TR_ProcurementSourcingCaseLots_NoMutation"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.LotCode).HasMaxLength(50).IsRequired().IsUnicode(false);
        builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired().IsUnicode(false);
        builder.HasIndex(item => new { item.TenantId, item.SourcingCaseId, item.LotNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SourcingCaseId, item.LotCode }).IsUnique();
        builder.HasOne(item => item.SourcingCase).WithMany(item => item.Lots)
            .HasForeignKey(item => item.SourcingCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSourcingCaseLotItemConfiguration : IEntityTypeConfiguration<ProcurementSourcingCaseLotItem>
{
    public void Configure(EntityTypeBuilder<ProcurementSourcingCaseLotItem> builder)
    {
        builder.ToTable("ProcurementSourcingCaseLotItems", table => table.HasTrigger("TR_ProcurementSourcingCaseLotItems_NoMutation"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.SourcingCaseId, item.PurchaseRequisitionItemId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.LotId, item.PurchaseRequisitionItemId }).IsUnique();
        builder.HasOne(item => item.SourcingCase).WithMany().HasForeignKey(item => item.SourcingCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Lot).WithMany(item => item.Items).HasForeignKey(item => item.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PurchaseRequisitionItem).WithMany().HasForeignKey(item => item.PurchaseRequisitionItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSourcingCaseSourceRequestConfiguration : IEntityTypeConfiguration<ProcurementSourcingCaseSourceRequest>
{
    public void Configure(EntityTypeBuilder<ProcurementSourcingCaseSourceRequest> builder)
    {
        builder.ToTable("ProcurementSourcingCaseSourceRequests", table => table.HasTrigger("TR_ProcurementSourcingCaseSourceRequests_Lifecycle"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RequestReference).HasMaxLength(100).IsRequired().IsUnicode(false);
        builder.Property(item => item.SourceType).HasMaxLength(50).IsRequired().IsUnicode(false);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.RequestReference }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SourcingCaseId, item.RequestSequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SourceType, item.SourceEntityId }).IsUnique()
            .HasFilter("[SourceEntityId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne(item => item.SourcingCase).WithMany(item => item.SourceRequests)
            .HasForeignKey(item => item.SourcingCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
