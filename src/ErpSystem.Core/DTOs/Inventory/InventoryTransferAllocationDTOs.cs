using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryTransitStockReportDto
{
    public List<InventoryTransitStockRowDto> Items { get; set; } = new();
    public int LegacyReconciliationRequiredCount { get; set; }
}

public sealed class InventoryTransitStockRowDto
{
    public Guid InTransitWarehouseId { get; set; }
    public string InTransitWarehouseName { get; set; } = string.Empty;
    public decimal ReturnedQuantity { get; set; }
    public Guid TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid TransferItemId { get; set; }
    public Guid DispatchAllocationId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid SourceWarehouseId { get; set; }
    public string SourceWarehouseName { get; set; } = string.Empty;
    public Guid DestinationWarehouseId { get; set; }
    public string DestinationWarehouseName { get; set; } = string.Empty;
    public Guid SourceLocationId { get; set; }
    public string SourceLocationName { get; set; } = string.Empty;
    public Guid InTransitLocationId { get; set; }
    public string InTransitLocationName { get; set; } = string.Empty;
    public Guid? CarrierBusinessPartnerId { get; set; }
    public string? CarrierName { get; set; }
    public string? VehicleNumber { get; set; }
    public DateTime ShippedAtUtc { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal InTransitQuantity { get; set; }
    public decimal InTransitValue { get; set; }
}

public sealed class InventoryTransferPickRequest
{
    [Required] public Guid SourceLocationId { get; set; }
    [Range(typeof(decimal), "0.0001", "99999999999999.9999")] public decimal Quantity { get; set; }
}

public sealed class InventoryTransferReceiptAllocationRequest
{
    [Required] public Guid DispatchAllocationId { get; set; }
    [Required] public Guid DestinationLocationId { get; set; }
    [Range(typeof(decimal), "0.0001", "99999999999999.9999")] public decimal ReceivedQuantity { get; set; }
}

public sealed class InventoryTransferPickingOptionDto
{
    public Guid ItemId { get; set; }
    public Guid SourceLocationId { get; set; }
    public string SourceLocationName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityAvailable { get; set; }
}

public sealed class InventoryTransferDispatchAllocationDto
{
    public Guid Id { get; set; }
    public Guid SourceLocationId { get; set; }
    public string SourceLocationName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal OutstandingQuantity { get; set; }
    public Guid? CarrierBusinessPartnerId { get; set; }
    public string? CarrierName { get; set; }
    public string? VehicleNumber { get; set; }
    public DateTime ShippedAtUtc { get; set; }
}
