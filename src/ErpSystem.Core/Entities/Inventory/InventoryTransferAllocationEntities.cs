using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Inventory;

/// <summary>Immutable physical source pick for one governed dispatch action.</summary>
public sealed class InventoryTransferDispatchAllocation : TenantEntity
{
    public Guid InventoryTransferActionId { get; set; }
    public Guid InventoryTransferItemId { get; set; }
    public Guid SourceLocationId { get; set; }
    public Guid SourceInventoryWarehouseId { get; set; }
    public Guid InTransitLocationId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    public Guid? CarrierBusinessPartnerId { get; set; }
    [MaxLength(200)] public string? CarrierName { get; set; }
    [MaxLength(50)] public string? CarrierAccountNumber { get; set; }
    [MaxLength(100)] public string? VehicleNumber { get; set; }
    [MaxLength(100)] public string? TrackingNumber { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string TrackingSnapshotJson { get; set; } = "[]";
    public InventoryTransferAction InventoryTransferAction { get; set; } = null!;
    public InventoryTransferItem InventoryTransferItem { get; set; } = null!;
    public WarehouseLocation SourceLocation { get; set; } = null!;
    public Warehouse SourceInventoryWarehouse { get; set; } = null!;
    public WarehouseLocation InTransitLocation { get; set; } = null!;
    public BusinessPartner? CarrierBusinessPartner { get; set; }
}

/// <summary>Immutable destination or return allocation against an original physical pick.</summary>
public sealed class InventoryTransferReceiptAllocation : TenantEntity
{
    [Column(TypeName = "nvarchar(max)")] public string TrackingSnapshotJson { get; set; } = "[]";
    public Guid InventoryTransferActionId { get; set; }
    public Guid DispatchAllocationId { get; set; }
    public Guid DestinationLocationId { get; set; }
    public Guid DestinationInventoryWarehouseId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    public bool ReturnedToSource { get; set; }
    public InventoryTransferAction InventoryTransferAction { get; set; } = null!;
    public InventoryTransferDispatchAllocation DispatchAllocation { get; set; } = null!;
    public WarehouseLocation DestinationLocation { get; set; } = null!;
    public Warehouse DestinationInventoryWarehouse { get; set; } = null!;
}
