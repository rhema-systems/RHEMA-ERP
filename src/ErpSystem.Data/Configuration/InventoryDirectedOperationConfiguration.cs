using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryDirectedTaskConfiguration : IEntityTypeConfiguration<InventoryDirectedTask>
{
    public void Configure(EntityTypeBuilder<InventoryDirectedTask> builder)
    {
        builder.ToTable("InventoryDirectedTasks", table =>
        {
            table.HasCheckConstraint("CK_InventoryDirectedTasks_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_InventoryDirectedTasks_TaskType", "[TaskType] IN (1,2,3)");
            table.HasCheckConstraint("CK_InventoryDirectedTasks_Status", "[Status] IN (1,2,3,4,5)");
            table.HasCheckConstraint("CK_InventoryDirectedTasks_Hashes", "LEN([SuggestionKey]) = 64 AND LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64");
        });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.TaskNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SuggestionKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.WarehouseId, item.Status, item.TaskType });
        builder.HasIndex(item => new { item.TenantId, item.SourceDocumentType, item.SourceDocumentId, item.SourceLineId });
        builder.HasOne(item => item.Warehouse).WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.InventoryItem).WithMany().HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourceLocation).WithMany().HasForeignKey(item => item.SourceLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.DestinationLocation).WithMany().HasForeignKey(item => item.DestinationLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AssignedToUser).WithMany().HasForeignKey(item => item.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.CreatedByUser).WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.LinkedInventoryTransfer).WithMany().HasForeignKey(item => item.LinkedInventoryTransferId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryDirectedTaskActionConfiguration : IEntityTypeConfiguration<InventoryDirectedTaskAction>
{
    public void Configure(EntityTypeBuilder<InventoryDirectedTaskAction> builder)
    {
        builder.ToTable("InventoryDirectedTaskActions", table =>
        {
            table.HasCheckConstraint("CK_InventoryDirectedTaskActions_Sequence", "[Sequence] > 0");
            table.HasCheckConstraint("CK_InventoryDirectedTaskActions_IntegrityHash", "LEN([IntegrityHash]) = 64");
            table.HasCheckConstraint("CK_InventoryDirectedTaskActions_ActionType", "[ActionType] IN (1,2,3,4,5,6,7)");
            table.HasCheckConstraint("CK_InventoryDirectedTaskActions_StatusAfter", "[StatusAfter] IN (1,2,3,4,5)");
        });
        builder.HasIndex(item => new { item.TenantId, item.TaskId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.OccurredAtUtc });
        builder.HasOne(item => item.Task).WithMany(item => item.Actions).HasForeignKey(item => item.TaskId).OnDelete(DeleteBehavior.Cascade);
    }
}
