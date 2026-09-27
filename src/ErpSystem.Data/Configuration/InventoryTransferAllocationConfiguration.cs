using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryTransferDispatchAllocationConfiguration : IEntityTypeConfiguration<InventoryTransferDispatchAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryTransferDispatchAllocation> builder)
    {
        builder.ToTable("InventoryTransferDispatchAllocations", table =>
        {
            table.HasTrigger("TR_InventoryTransferDispatchAllocations_Guard");
            table.HasCheckConstraint("CK_InventoryTransferDispatchAllocations_Quantity", "[Quantity]>0");
        });
        builder.HasIndex(x => new { x.TenantId, x.InventoryTransferActionId, x.InventoryTransferItemId, x.SourceLocationId }).IsUnique();
        builder.HasOne(x => x.InventoryTransferAction).WithMany().HasForeignKey(x => x.InventoryTransferActionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InventoryTransferItem).WithMany().HasForeignKey(x => x.InventoryTransferItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceLocation).WithMany().HasForeignKey(x => x.SourceLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceInventoryWarehouse).WithMany().HasForeignKey(x => x.SourceInventoryWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InTransitLocation).WithMany().HasForeignKey(x => x.InTransitLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CarrierBusinessPartner).WithMany().HasForeignKey(x => x.CarrierBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransferReceiptAllocationConfiguration : IEntityTypeConfiguration<InventoryTransferReceiptAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryTransferReceiptAllocation> builder)
    {
        builder.ToTable("InventoryTransferReceiptAllocations", table =>
        {
            table.HasTrigger("TR_InventoryTransferReceiptAllocations_Guard");
            table.HasCheckConstraint("CK_InventoryTransferReceiptAllocations_Quantity", "[Quantity]>0");
        });
        builder.HasIndex(x => new { x.TenantId, x.InventoryTransferActionId, x.DispatchAllocationId, x.DestinationLocationId }).IsUnique();
        builder.HasOne(x => x.InventoryTransferAction).WithMany().HasForeignKey(x => x.InventoryTransferActionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DispatchAllocation).WithMany().HasForeignKey(x => x.DispatchAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationLocation).WithMany().HasForeignKey(x => x.DestinationLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationInventoryWarehouse).WithMany().HasForeignKey(x => x.DestinationInventoryWarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransferMovementAllocationConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.HasOne(x => x.TransferDispatchAllocation).WithMany().HasForeignKey(x => x.TransferDispatchAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TransferReceiptAllocation).WithMany().HasForeignKey(x => x.TransferReceiptAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.TransferDispatchAllocationId, x.TransferLeg }).IsUnique()
            .HasFilter("[TransferDispatchAllocationId] IS NOT NULL AND [TransferReceiptAllocationId] IS NULL");
        builder.HasIndex(x => new { x.TenantId, x.TransferReceiptAllocationId, x.TransferLeg }).IsUnique()
            .HasFilter("[TransferReceiptAllocationId] IS NOT NULL");
        builder.ToTable("InventoryMovements", table => table.HasTrigger("TR_InventoryMovements_TransferLegGuard"));
    }
}

public sealed class InventoryTransferStockProjectionAllocationConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasOne(x => x.TransferDispatchAllocation).WithMany().HasForeignKey(x => x.TransferDispatchAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TransferReceiptAllocation).WithMany().HasForeignKey(x => x.TransferReceiptAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.TransferDispatchAllocationId, x.TransferLeg }).IsUnique()
            .HasFilter("[TransferDispatchAllocationId] IS NOT NULL AND [TransferReceiptAllocationId] IS NULL");
        builder.HasIndex(x => new { x.TenantId, x.TransferReceiptAllocationId, x.TransferLeg }).IsUnique()
            .HasFilter("[TransferReceiptAllocationId] IS NOT NULL");
        builder.ToTable("StockMovements", table => table.HasTrigger("TR_StockMovements_TransferLegGuard"));
    }
}

public sealed class InventoryTransitWarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder) =>
        builder.ToTable("Warehouses", table => table.HasTrigger("TR_Warehouses_TransitIdentity"));
}

public sealed class InventoryTransitLocationConfiguration : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder) =>
        builder.ToTable("WarehouseLocations", table => table.HasTrigger("TR_WarehouseLocations_TransitIdentity"));
}
