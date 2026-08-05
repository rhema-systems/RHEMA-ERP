using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryValuationReconciliationConfiguration
    : IEntityTypeConfiguration<InventoryValuationReconciliation>
{
    public void Configure(EntityTypeBuilder<InventoryValuationReconciliation> builder)
    {
        builder.ToTable("InventoryValuationReconciliations", table =>
            table.HasTrigger("TR_InventoryValuationReconciliations_Guard"));
        builder.HasIndex(value => new { value.TenantId, value.ReconciliationNumber }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.FiscalPeriodId, value.GeneratedAtUtc });
        builder.HasIndex(value => new { value.TenantId, value.FiscalPeriodId, value.IdempotencyKey }).IsUnique();
        builder.Property(value => value.Status).HasConversion<int>();
        builder.HasOne(value => value.FiscalPeriod).WithMany().HasForeignKey(value => value.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.InventoryControlAccount).WithMany()
            .HasForeignKey(value => value.InventoryControlAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.PeriodModuleLock).WithMany()
            .HasForeignKey(value => value.PeriodModuleLockId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryValuationReconciliationActionConfiguration
    : IEntityTypeConfiguration<InventoryValuationReconciliationAction>
{
    public void Configure(EntityTypeBuilder<InventoryValuationReconciliationAction> builder)
    {
        builder.ToTable("InventoryValuationReconciliationActions", table =>
            table.HasTrigger("TR_InventoryValuationReconciliationActions_Immutable"));
        builder.HasIndex(value => new { value.TenantId, value.ReconciliationId, value.Sequence }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.ReconciliationId, value.IdempotencyKey }).IsUnique();
        builder.Property(value => value.ActionType).HasConversion<int>();
        builder.Property(value => value.PreviousStatus).HasConversion<int?>();
        builder.Property(value => value.NewStatus).HasConversion<int>();
        builder.HasOne(value => value.Reconciliation).WithMany(value => value.Actions)
            .HasForeignKey(value => value.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
    }
}
