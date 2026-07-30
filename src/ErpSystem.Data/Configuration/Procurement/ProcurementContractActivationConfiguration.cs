using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Procurement;

public sealed class ProcurementContractActivationConfiguration :
    IEntityTypeConfiguration<ProcurementContractActivation>
{
    public void Configure(EntityTypeBuilder<ProcurementContractActivation> builder)
    {
        builder.ToTable("ProcurementContractActivations", table =>
            table.HasTrigger("TR_ProcurementContractActivations_TDC0407Protected"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.ContractId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ContractId, item.Status });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasCheckConstraint("CK_ProcurementContractActivations_Sequence", "[Sequence] >= 1");
        builder.HasCheckConstraint("CK_ProcurementContractActivations_Hashes",
            "LEN([AwardReadinessIntegrityHash]) = 64 AND LEN([GhanepsConfigurationValueHash]) = 64 AND LEN([ContractSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([ContractSnapshotJson]) = 1 AND ISJSON([ReadinessSnapshotJson]) = 1");
        builder.HasOne(item => item.Contract).WithMany()
            .HasForeignKey(item => item.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AwardReadinessDecision).WithMany()
            .HasForeignKey(item => item.AwardReadinessDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PerformanceBondRequest).WithMany()
            .HasForeignKey(item => item.PerformanceBondRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementContractActivationEvidenceConfiguration :
    IEntityTypeConfiguration<ProcurementContractActivationEvidence>
{
    public void Configure(EntityTypeBuilder<ProcurementContractActivationEvidence> builder)
    {
        builder.ToTable("ProcurementContractActivationEvidence", table =>
            table.HasTrigger("TR_ProcurementContractActivationEvidence_TDC0407Immutable"));
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.TenantId, item.ActivationId, item.RequirementKey }).IsUnique();
        builder.HasCheckConstraint("CK_ProcurementContractActivationEvidence_Reference",
            "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)");
        builder.HasCheckConstraint("CK_ProcurementContractActivationEvidence_Hash",
            "LEN([EvidenceHash]) = 64");
        builder.HasOne(item => item.Activation).WithMany(item => item.Evidence)
            .HasForeignKey(item => item.ActivationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
            .HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.FileUploadRecord).WithMany()
            .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
