using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementSupplierAvlRegisterConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierAvlRegister>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierAvlRegister> builder)
    {
        builder.ToTable("ProcurementSupplierAvlRegisters", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierAvlRegisters_Lifecycle");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierAvlRegisters_State",
                "[Version] >= 1 AND [ReviewYear] BETWEEN 2000 AND 9999 " +
                "AND [Status] BETWEEN 0 AND 5 AND [ExpiresAtUtc] > [EffectiveFromUtc] " +
                "AND ([ScheduledRetirementAtUtc] IS NULL OR [ScheduledRetirementAtUtc] >= [EffectiveFromUtc]) " +
                "AND [ReviewFrequencyMonths] BETWEEN 1 AND 120 " +
                "AND [PolicyProfileVersion] >= 1 AND LEN([PolicyValueHash]) = 64 " +
                "AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 " +
                "AND ISJSON([SnapshotJson]) = 1 " +
                "AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL) OR [Status] <> 1) " +
                "AND (([Status] IN (2,3,5) AND [ApprovedById] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL) OR [Status] NOT IN (2,3,5)) " +
                "AND (([Status] = 3 AND [PublishedById] IS NOT NULL AND [PublishedAtUtc] IS NOT NULL) OR [Status] <> 3) " +
                "AND (([Status] = 4 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 4) " +
                "AND (([Status] = 5 AND [RetiredAtUtc] IS NOT NULL) OR [Status] <> 5)");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.RegisterCode, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ReviewYear })
            .IsUnique().HasFilter("[Status] IN (0,1,2) AND [IsDeleted] = 0")
            .HasDatabaseName("UX_ProcurementSupplierAvlRegisters_OpenReview");
        builder.HasIndex(item => new { item.TenantId, item.Status, item.EffectiveFromUtc, item.ExpiresAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.PolicyDecisionId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.PolicyDecision).WithMany()
            .HasForeignKey(item => item.PolicyDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PolicyProfile).WithMany()
            .HasForeignKey(item => item.PolicyProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SupersededByRegister).WithMany()
            .HasForeignKey(item => item.SupersededByRegisterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierAvlEntryConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierAvlEntry>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierAvlEntry> builder)
    {
        builder.ToTable("ProcurementSupplierAvlEntries", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierAvlEntries_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierAvlEntries_State",
                "[Status] BETWEEN 0 AND 2 AND LEN([EligibilityDecisionHash]) = 64 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([EligibilitySnapshotJson]) = 1 " +
                "AND (([Status] = 1 AND [SuspendedAtUtc] IS NOT NULL AND [SuspendedById] IS NOT NULL " +
                "AND LEN([SuspensionReason]) > 0) OR [Status] <> 1) " +
                "AND (([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL) OR [Status] <> 2)");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.BusinessPartnerId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.DueDiligenceReviewId });

        builder.HasOne(item => item.Register).WithMany(item => item.Entries)
            .HasForeignKey(item => item.RegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.DueDiligenceReview).WithMany()
            .HasForeignKey(item => item.DueDiligenceReviewId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Registration).WithMany()
            .HasForeignKey(item => item.RegistrationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.EvidencePackVersion).WithMany()
            .HasForeignKey(item => item.EvidencePackVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.QualifiedListEntry).WithMany()
            .HasForeignKey(item => item.QualifiedListEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierAvlEntryStatusHistoryConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierAvlEntryStatusHistory>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierAvlEntryStatusHistory> builder)
    {
        builder.ToTable("ProcurementSupplierAvlEntryStatusHistories", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierAvlEntryStatusHistories_AppendOnly");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierAvlEntryStatusHistories_State",
                "[Action] BETWEEN 0 AND 2 AND [BeforeStatus] BETWEEN 0 AND 2 " +
                "AND [AfterStatus] BETWEEN 0 AND 2 AND [BeforeStatus] <> [AfterStatus] " +
                "AND LEN([Reason]) > 0 AND LEN([CorrelationId]) > 0 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([EvidenceJson]) = 1 " +
                "AND ISJSON([SnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.CorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.EntryId, item.OccurredAtUtc });
        builder.HasOne(item => item.Register).WithMany()
            .HasForeignKey(item => item.RegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Entry).WithMany(item => item.StatusHistory)
            .HasForeignKey(item => item.EntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementSupplierAvlPublicationSnapshotConfiguration :
    IEntityTypeConfiguration<ProcurementSupplierAvlPublicationSnapshot>
{
    public void Configure(EntityTypeBuilder<ProcurementSupplierAvlPublicationSnapshot> builder)
    {
        builder.ToTable("ProcurementSupplierAvlPublicationSnapshots", table =>
        {
            table.HasTrigger("TR_ProcurementSupplierAvlPublicationSnapshots_AppendOnly");
            table.HasCheckConstraint(
                "CK_ProcurementSupplierAvlPublicationSnapshots_State",
                "[Sequence] >= 1 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1");
        });

        builder.HasIndex(item => new { item.TenantId, item.RegisterId, item.Sequence }).IsUnique();
        builder.HasOne(item => item.Register).WithMany(item => item.PublicationSnapshots)
            .HasForeignKey(item => item.RegisterId).OnDelete(DeleteBehavior.Restrict);
    }
}
