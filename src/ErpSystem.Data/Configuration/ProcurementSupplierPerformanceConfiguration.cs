using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementSupplierPerformanceScorecardConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierPerformanceScorecard>
{
    public void Configure(
        EntityTypeBuilder<ProcurementSupplierPerformanceScorecard> builder)
    {
        builder.ToTable("ProcurementSupplierPerformanceScorecards", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierPerformanceScorecards_AppendOnly");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierPerformanceScorecards_State",
                "[ScorecardSequence] >= 1 AND [PeriodEndUtc] >= [PeriodStartUtc] " +
                "AND [NextReviewDueAtUtc] > [CalculatedAtUtc] " +
                "AND [PerformanceWindowMonths] BETWEEN 1 AND 120 " +
                "AND [MinimumScore] BETWEEN 0 AND 100 " +
                "AND [MinimumDataCoveragePercent] BETWEEN 1 AND 100 " +
                "AND [ResponseTargetHours] BETWEEN 1 AND 8760 " +
                "AND [EligibilityAction] BETWEEN 0 AND 2 AND [DataStatus] BETWEEN 0 AND 2 " +
                "AND [DataCoveragePercent] BETWEEN 0 AND 100 " +
                "AND ([OverallScore] IS NULL OR [OverallScore] BETWEEN 0 AND 100) " +
                "AND ([DeliveryTimelinessScore] IS NULL OR [DeliveryTimelinessScore] BETWEEN 0 AND 100) " +
                "AND ([GrnQualityScore] IS NULL OR [GrnQualityScore] BETWEEN 0 AND 100) " +
                "AND ([RejectionRateScore] IS NULL OR [RejectionRateScore] BETWEEN 0 AND 100) " +
                "AND ([PriceCompetitivenessScore] IS NULL OR [PriceCompetitivenessScore] BETWEEN 0 AND 100) " +
                "AND ([ResponsivenessScore] IS NULL OR [ResponsivenessScore] BETWEEN 0 AND 100) " +
                "AND ([ComplaintResolutionScore] IS NULL OR [ComplaintResolutionScore] BETWEEN 0 AND 100) " +
                "AND ([ContractCompletionScore] IS NULL OR [ContractCompletionScore] BETWEEN 0 AND 100) " +
                "AND [PurchaseOrderCount] >= 0 AND [ReceiptCount] >= 0 " +
                "AND [ReceiptLineCount] >= 0 AND [PriceComparisonCount] >= 0 " +
                "AND [ResponseObservationCount] >= 0 AND [ComplaintCount] >= 0 " +
                "AND [ContractCount] >= 0 AND [PolicyProfileVersion] >= 1 " +
                "AND LEN([PolicyValueHash]) = 64 AND LEN([SourceSnapshotHash]) = 64 " +
                "AND LEN([SupplierEligibilityDecisionHash]) = 64 " +
                "AND ([RiskAssessmentIntegrityHash] IS NULL OR LEN([RiskAssessmentIntegrityHash]) = 64) " +
                "AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 " +
                "AND ISJSON([MeasureResultsJson]) = 1 AND ISJSON([SourceSnapshotJson]) = 1 " +
                "AND ISJSON([SupplierControlSnapshotJson]) = 1 " +
                "AND ISJSON([FindingsJson]) = 1 AND ISJSON([SnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.ScorecardReference })
            .IsUnique();
        builder.HasIndex(item => new
            {
                item.TenantId,
                item.BusinessPartnerId,
                item.ScorecardSequence
            }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(item => new
            {
                item.TenantId,
                item.BusinessPartnerId,
                item.CalculatedAtUtc
            });
        builder.HasIndex(item => new { item.TenantId, item.PolicyDecisionId });
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicyDecision).WithMany()
            .HasForeignKey(item => item.PolicyDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicyProfile).WithMany()
            .HasForeignKey(item => item.PolicyProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RiskAssessment).WithMany()
            .HasForeignKey(item => item.RiskAssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
