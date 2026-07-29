using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementFrameworkCallOffConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkCallOff>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkCallOff> builder)
    {
        builder.ToTable("ProcurementFrameworkCallOffs", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkCallOffs_Lifecycle");
            table.HasTrigger("TR_ProcurementFrameworkCallOffs_PurchaseOrderSource");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkCallOffs_State",
                "[Status] BETWEEN 0 AND 5 AND [AuthorityKind] BETWEEN 0 AND 3 " +
                "AND [AgreementVersion] >= 1 AND [PriceListVersion] >= 1 " +
                "AND [TotalAmount] > 0 AND LEN([CurrencyCode]) = 3 " +
                "AND [RequiredDateUtc] >= [AgreementEffectiveFromUtc] " +
                "AND [RequiredDateUtc] <= [AgreementEffectiveEndUtc] " +
                "AND LEN([AgreementIntegrityHash]) = 64 " +
                "AND LEN([AwardReadinessIntegrityHash]) = 64 " +
                "AND LEN([SupplierEligibilityDecisionHash]) = 64 " +
                "AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1 " +
                "AND LEN([CreationCorrelationId]) > 0 " +
                "AND LEN([LastOperationCorrelationId]) > 0 " +
                "AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL " +
                "AND [WorkflowDefinitionId] IS NOT NULL AND [WorkflowInstanceId] IS NOT NULL) OR [Status] <> 1) " +
                "AND (([Status] IN (2, 3) AND [ApprovedById] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL " +
                "AND [BalanceDeductedAtUtc] IS NOT NULL) OR [Status] NOT IN (2, 3)) " +
                "AND (([Status] = 3 AND [IssuedById] IS NOT NULL AND [IssuedAtUtc] IS NOT NULL) OR [Status] <> 3) " +
                "AND (([Status] = 4 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 4) " +
                "AND (([Status] = 5 AND [CancelledById] IS NOT NULL AND [CancelledAtUtc] IS NOT NULL) OR [Status] <> 5)");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.TenantId, item.CallOffNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CreationCorrelationId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AgreementId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.SourceRequisitionId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.Status });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });

        builder.HasOne(item => item.Agreement).WithMany(item => item.CallOffs)
            .HasForeignKey(item => item.AgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PurchaseOrder).WithMany()
            .HasForeignKey(item => item.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourceRequisition).WithMany()
            .HasForeignKey(item => item.SourceRequisitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany()
            .HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Authority).WithMany()
            .HasForeignKey(item => item.AuthorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany()
            .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany()
            .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkCallOffLineConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkCallOffLine>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkCallOffLine> builder)
    {
        builder.ToTable("ProcurementFrameworkCallOffLines", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkCallOffLines_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkCallOffLines_State",
                "[Quantity] > 0 AND [UnitPrice] > 0 AND [LineTotal] > 0 " +
                "AND [SourceDemandQuantity] > 0 AND [Quantity] <= [SourceDemandQuantity] " +
                "AND LEN([ItemCode]) > 0 AND LEN([ItemName]) > 0 " +
                "AND LEN([UnitOfMeasure]) > 0 AND LEN([PriceIntegrityHash]) = 64 " +
                "AND LEN([IntegrityHash]) = 64");
        });

        builder.HasIndex(item => new { item.TenantId, item.CallOffId, item.PurchaseRequisitionItemId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CallOffId, item.AgreementPriceLineId })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseOrderItemId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PurchaseRequisitionItemId });

        builder.HasOne(item => item.CallOff).WithMany(item => item.Lines)
            .HasForeignKey(item => item.CallOffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AgreementPriceLine).WithMany()
            .HasForeignKey(item => item.AgreementPriceLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PurchaseRequisitionItem).WithMany()
            .HasForeignKey(item => item.PurchaseRequisitionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.PurchaseOrderItem).WithMany()
            .HasForeignKey(item => item.PurchaseOrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.InventoryItem).WithMany()
            .HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkAgreementBalanceConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkAgreementBalance>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkAgreementBalance> builder)
    {
        builder.ToTable("ProcurementFrameworkAgreementBalances", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkAgreementBalances_Protected");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkAgreementBalances_State",
                "[CeilingAmount] > 0 AND [CommittedAmount] >= 0 " +
                "AND [IssuedAmount] >= 0 AND [IssuedAmount] <= [CommittedAmount] " +
                "AND [AvailableAmount] >= 0 " +
                "AND [CommittedAmount] + [AvailableAmount] = [CeilingAmount] " +
                "AND LEN([CurrencyCode]) = 3 AND LEN([IntegrityHash]) = 64");
        });

        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.AgreementId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.LastExpiryAlertAtUtc });
        builder.HasOne(item => item.Agreement).WithOne(item => item.Balance)
            .HasForeignKey<ProcurementFrameworkAgreementBalance>(item => item.AgreementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementFrameworkBalanceMovementConfiguration :
    IEntityTypeConfiguration<ProcurementFrameworkBalanceMovement>
{
    public void Configure(EntityTypeBuilder<ProcurementFrameworkBalanceMovement> builder)
    {
        builder.ToTable("ProcurementFrameworkBalanceMovements", table =>
        {
            table.HasTrigger("TR_ProcurementFrameworkBalanceMovements_AppendOnly");
            table.HasCheckConstraint(
                "CK_ProcurementFrameworkBalanceMovements_State",
                "[MovementType] BETWEEN 0 AND 2 AND [Amount] > 0 " +
                "AND [BalanceBefore] >= 0 AND [BalanceAfter] >= 0 " +
                "AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0 " +
                "AND LEN([ActorName]) > 0 AND LEN([IntegrityHash]) = 64");
        });

        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.CallOffId, item.MovementType })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.AgreementId, item.OccurredAtUtc });
        builder.HasOne(item => item.Agreement).WithMany()
            .HasForeignKey(item => item.AgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AgreementBalance).WithMany(item => item.Movements)
            .HasForeignKey(item => item.AgreementBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CallOff).WithMany(item => item.BalanceMovements)
            .HasForeignKey(item => item.CallOffId).OnDelete(DeleteBehavior.Restrict);
    }
}
