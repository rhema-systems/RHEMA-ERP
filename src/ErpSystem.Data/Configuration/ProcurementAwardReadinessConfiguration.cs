using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementAwardReadinessDecisionConfiguration :
    IEntityTypeConfiguration<ProcurementAwardReadinessDecision>
{
    public void Configure(EntityTypeBuilder<ProcurementAwardReadinessDecision> builder)
    {
        builder.ToTable("ProcurementAwardReadinessDecisions", table =>
        {
            table.HasTrigger("TR_ProcurementAwardReadinessDecisions_Immutable");
            table.HasTrigger("TR_ProcurementAwardReadinessDecisions_RfqApprovalPolicy");
            table.HasTrigger("TR_ProcurementAwardReadinessDecisions_TenderApprovalPolicy");
            table.HasTrigger("TR_ProcurementAwardReadinessDecisions_ExceptionalApprovalPolicy");
            table.HasCheckConstraint("CK_ProcurementAwardReadinessDecisions_State",
                "[SourceType] BETWEEN 0 AND 2 AND [Method] BETWEEN 0 AND 8 " +
                "AND [DecisionSequence] >= 1 AND [Status] BETWEEN 0 AND 1 " +
                "AND LEN([SourceIntegrityHash]) = 64 AND LEN([IntegrityHash]) = 64 " +
                "AND ISJSON([RecommendedSubjectIdsJson]) = 1 " +
                "AND ISJSON([RecommendedBusinessPartnerIdsJson]) = 1 " +
                "AND ISJSON([RecommendationSnapshotJson]) = 1 " +
                "AND ISJSON([EvaluationLineageJson]) = 1 " +
                "AND ISJSON([SupplierLineageJson]) = 1 " +
                "AND ISJSON([PrequalificationLineageJson]) = 1 " +
                "AND ISJSON([VerificationLineageJson]) = 1 " +
                "AND ISJSON([AuthorityLineageJson]) = 1 " +
                "AND ISJSON([EvidenceLineageJson]) = 1 " +
                "AND ISJSON([PrerequisiteSnapshotJson]) = 1 " +
                "AND ISJSON([TimelineSnapshotJson]) = 1 " +
                "AND ISJSON([BlockedReasonsJson]) = 1");
        });

        builder.HasIndex(item => new
            { item.TenantId, item.SourceType, item.SourceId, item.DecisionSequence }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.SourceType, item.SourceId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new
            { item.TenantId, item.SourceType, item.SourceId, item.EvaluatedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.Status, item.EvaluatedAtUtc });
    }
}
