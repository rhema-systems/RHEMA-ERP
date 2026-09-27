using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryCycleCountScheduleConfiguration : IEntityTypeConfiguration<InventoryCycleCountSchedule>
{
    public void Configure(EntityTypeBuilder<InventoryCycleCountSchedule> builder)
    {
        builder.ToTable("InventoryCycleCountSchedules", table =>
        {
            table.HasTrigger("TR_InventoryCycleCountSchedules_ControlledLifecycle");
            table.HasCheckConstraint("CK_InventoryCycleCountSchedules_ABCClass", "[ABCClass] IN ('A','B','C')");
            table.HasCheckConstraint("CK_InventoryCycleCountSchedules_Frequency", "[FrequencyDays] BETWEEN 1 AND 366");
            table.HasCheckConstraint("CK_InventoryCycleCountSchedules_Thresholds", "[RecountQuantityThreshold] >= 0 AND [RecountValueThreshold] >= 0");
            table.HasCheckConstraint("CK_InventoryCycleCountSchedules_Cutoff", "[NextDueAtUtc] <= [CutoffAtUtc]");
        });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.WarehouseId, x.LocationId, x.ABCClass })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
        builder.HasIndex(x => new { x.TenantId, x.IsActive, x.NextDueAtUtc });
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementCalendarOccurrence>().WithMany().HasForeignKey(x => x.CalendarOccurrenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementCalendarOccurrence>().WithMany().HasForeignKey(x => x.CutoffOccurrenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LastPhysicalCount).WithMany().HasForeignKey(x => x.LastPhysicalCountId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class PhysicalCountControlConfiguration : IEntityTypeConfiguration<PhysicalCount>
{
    public void Configure(EntityTypeBuilder<PhysicalCount> builder)
    {
        builder.Navigation(x => x.Counters).AutoInclude();
        builder.HasOne<PhysicalCount>().WithMany().HasForeignKey(x => x.RootPhysicalCountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<PhysicalCount>().WithMany().HasForeignKey(x => x.ParentPhysicalCountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.TenantId, x.ParentPhysicalCountId, x.RecountRequestKey }).IsUnique().HasFilter("[ParentPhysicalCountId] IS NOT NULL AND [RecountRequestKey] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.RootPhysicalCountId, x.RecountAttempt }).IsUnique().HasFilter("[RootPhysicalCountId] IS NOT NULL");
        builder.ToTable("PhysicalCounts", table =>
        {
            table.HasTrigger("TR_PhysicalCounts_ControlledLifecycle");
            table.HasTrigger("TR_PhysicalCounts_CommitteeActors");
            table.HasTrigger("TR_PhysicalCounts_RecountLineage");
            table.HasCheckConstraint("CK_PhysicalCounts_ABCClass", "[ABCClass] IS NULL OR [ABCClass] IN ('A','B','C')");
            table.HasCheckConstraint("CK_PhysicalCounts_Cutoff", "[ScheduledForUtc] IS NULL OR [CutoffAtUtc] IS NULL OR [ScheduledForUtc] <= [CutoffAtUtc]");
        });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.CycleCountScheduleId, x.ScheduledForUtc })
            .IsUnique().HasFilter("[CycleCountScheduleId] IS NOT NULL AND [ScheduledForUtc] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => x.StockAdjustmentId).IsUnique().HasFilter("[StockAdjustmentId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne<InventoryCycleCountSchedule>().WithMany().HasForeignKey(x => x.CycleCountScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementCalendarOccurrence>().WithMany().HasForeignKey(x => x.CalendarOccurrenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementCalendarOccurrence>().WithMany().HasForeignKey(x => x.CutoffOccurrenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StockAdjustment).WithMany().HasForeignKey(x => x.StockAdjustmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StoresApprovedBy).WithMany().HasForeignKey(x => x.StoresApprovedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.FinanceApprovedBy).WithMany().HasForeignKey(x => x.FinanceApprovedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AuditAttestedBy).WithMany().HasForeignKey(x => x.AuditAttestedById).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class PhysicalCountItemControlConfiguration : IEntityTypeConfiguration<PhysicalCountItem>
{
    public void Configure(EntityTypeBuilder<PhysicalCountItem> builder)
    {
        builder.ToTable("PhysicalCountItems", table =>
        {
            table.HasTrigger("TR_PhysicalCountItems_ControlledMutation");
            table.HasTrigger("TR_PhysicalCountItems_DefectiveObservation");
            table.HasTrigger("TR_PhysicalCountItems_RecountLineage");
            table.HasCheckConstraint("CK_PhysicalCountItems_DefectiveQuantity", "[DefectiveQuantity] >= 0 AND [DefectiveQuantity] <= [CountedQuantity]");
            table.HasCheckConstraint("CK_PhysicalCountItems_CountAttempts", "[CountAttempts] BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_PhysicalCountItems_CountQuantities", "[SystemQuantity] >= 0 AND [CountedQuantity] >= 0 AND ([FirstCountQuantity] IS NULL OR [FirstCountQuantity] >= 0) AND ([RecountedQuantity] IS NULL OR [RecountedQuantity] >= 0)");
        });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.RecountedBy).WithMany().HasForeignKey(x => x.RecountedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<PhysicalCountItem>().WithMany().HasForeignKey(x => x.RootPhysicalCountItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<PhysicalCountItem>().WithMany().HasForeignKey(x => x.PredecessorPhysicalCountItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<PhysicalCount>().WithMany().HasForeignKey(x => x.SupersededByPhysicalCountId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class PhysicalCountActionConfiguration : IEntityTypeConfiguration<PhysicalCountAction>
{
    public void Configure(EntityTypeBuilder<PhysicalCountAction> builder)
    {
        builder.ToTable("PhysicalCountActions", table =>
        {
            table.HasTrigger("TR_PhysicalCountActions_AppendOnly");
            table.HasCheckConstraint("CK_PhysicalCountActions_ActionType", "[ActionType] BETWEEN 1 AND 17");
        });
        builder.HasIndex(x => new { x.TenantId, x.PhysicalCountId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PhysicalCountId, x.ActionType, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.PhysicalCount).WithMany(x => x.Actions).HasForeignKey(x => x.PhysicalCountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PhysicalCountWarehouseQuantityConfiguration : IEntityTypeConfiguration<WarehouseQuantity>
{
    public void Configure(EntityTypeBuilder<WarehouseQuantity> builder) =>
        builder.ToTable("WarehouseQuantities", table => table.HasTrigger("TR_WarehouseQuantities_PhysicalCountFreeze"));
}

public sealed class PhysicalCountInventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder) =>
        builder.ToTable("InventoryItems", table => table.HasTrigger("TR_InventoryItems_PhysicalCountFreeze"));
}

public sealed class PhysicalCountStockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder) =>
        builder.ToTable("StockMovements", table => table.HasTrigger("TR_StockMovements_PhysicalCountFreeze"));
}
