using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Inventory;

#region Inventory Item DTOs

/// <summary>
/// Basic inventory item information for lists and searches
/// </summary>
public class InventoryItemDto
{
    public Guid Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal StandardCost { get; set; }
    public decimal AverageCost { get; set; }
    public bool IsSerialTracked { get; set; }
    public bool IsLotTracked { get; set; }
    public ItemType ItemType { get; set; }
    public ItemStatus Status { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

/// <summary>
/// Detailed inventory item information including locations and movements
/// </summary>
public class InventoryItemDetailDto : InventoryItemDto
{
    public decimal AllocatedStock { get; set; }
    public decimal OnOrderStock { get; set; }
    public decimal MinimumLevel { get; set; }
    public decimal MaximumLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal LastPurchaseCost { get; set; }
    public string? PrimarySupplier { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime? LastStockDate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public List<InventoryLocationDto> Locations { get; set; } = new();
    public List<StockMovementDto> RecentMovements { get; set; } = new();
}

/// <summary>
/// DTO for creating or updating inventory items
/// </summary>
public class CreateInventoryItemDto
{
    [Required]
    public string ItemCode { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public Guid CategoryId { get; set; }
    
    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    
    [Required]
    public string UnitOfMeasure { get; set; } = "EA";
    
    public ItemType ItemType { get; set; } = ItemType.StockItem;
    public ItemStatus Status { get; set; } = ItemStatus.Active;
    public decimal StandardCost { get; set; }
    public decimal MinimumLevel { get; set; }
    public decimal MaximumLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public int LeadTimeDays { get; set; } = 7;
    public bool IsSerialTracked { get; set; }
    public bool IsLotTracked { get; set; }
    public string? PrimarySupplier { get; set; }
}

#endregion

#region Stock Management DTOs

/// <summary>
/// Stock movement information
/// </summary>
public class StockMovementDto
{
    public Guid Id { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime MovementDate { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string? ProcessedBy { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public decimal RunningBalance { get; set; }
}

/// <summary>
/// DTO for creating stock movements
/// </summary>
public class CreateStockMovementDto
{
    [Required]
    public Guid InventoryItemId { get; set; }
    
    [Required]
    public string MovementType { get; set; } = string.Empty;
    
    [Required]
    public decimal Quantity { get; set; }
    
    public decimal UnitCost { get; set; }
    
    [Required]
    public ReferenceType ReferenceType { get; set; } = ReferenceType.Manual;
    
    public string? ReferenceNumber { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid? LocationId { get; set; }
    public string? Notes { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    
    [Required]
    public Guid ProcessedById { get; set; }
}

/// <summary>
/// Stock availability check result
/// </summary>
public class StockAvailabilityDto
{
    public Guid InventoryItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal OnOrderQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ReorderRequired { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
}

#endregion

#region Allocation DTOs

/// <summary>
/// Inventory allocation information
/// </summary>
public class InventoryAllocationDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string AllocationType { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public DateTime AllocationDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for allocating inventory
/// </summary>
public class AllocateInventoryDto
{
    [Required]
    public Guid InventoryItemId { get; set; }
    
    [Required]
    public decimal Quantity { get; set; }
    
    [Required]
    public string ReferenceNumber { get; set; } = string.Empty;
    
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Notes { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
}

#endregion

#region Location DTOs

/// <summary>
/// Inventory location information (item quantities at specific warehouse locations)
/// </summary>
public class InventoryLocationDto
{
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public DateTime? LastMovementDate { get; set; }
    public DateTime? LastCountDate { get; set; }
}

/// <summary>
/// Warehouse information
/// </summary>
public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public string WarehouseType { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Warehouse location information
/// </summary>
public class WarehouseLocationDto
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string LocationType { get; set; } = string.Empty;
    public Guid? ParentLocationId { get; set; }
    public bool IsActive { get; set; }
    public bool IsPickingLocation { get; set; }
    public bool IsReceivingLocation { get; set; }
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
    public int? MaxItems { get; set; }
    public decimal CurrentWeight { get; set; }
    public decimal CurrentVolume { get; set; }
    public int CurrentItemCount { get; set; }
}

#endregion

#region Purchase Order DTOs

/// <summary>
/// Purchase order summary for lists
/// </summary>
public class PurchaseOrderSummaryDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>
/// Detailed purchase order information
/// </summary>
public class PurchaseOrderDetailDto : PurchaseOrderSummaryDto
{
    public string? SupplierAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? PromisedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? RequestedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? ShippingTerms { get; set; }
    public string? Notes { get; set; }
    public string? DeliveryAddress { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// Purchase order item information
/// </summary>
public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? SupplierItemCode { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Basic purchase order DTO
/// </summary>
public class PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// DTO for creating purchase orders
/// </summary>
public class CreatePurchaseOrderDto
{
    [Required]
    public string SupplierName { get; set; } = string.Empty;
    
    public string? SupplierAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string? ShippingTerms { get; set; }
    public string? Notes { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    public string? DeliveryAddress { get; set; }
    
    [Required]
    public Guid RequestedById { get; set; }
    
    [Required]
    public List<CreatePurchaseOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for creating purchase order items
/// </summary>
public class CreatePurchaseOrderItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }
    
    public string? SupplierItemCode { get; set; }
    
    [Required]
    public decimal OrderedQuantity { get; set; }
    
    [Required]
    public decimal UnitPrice { get; set; }
    
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Purchase order receipt information
/// </summary>
public class PurchaseOrderReceiptDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public string? DeliveryNote { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReceivedByName { get; set; }
    public string? InspectedByName { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderReceiptItemDto> Items { get; set; } = new();
}

/// <summary>
/// Purchase order receipt item information
/// </summary>
public class PurchaseOrderReceiptItemDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? LocationCode { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// DTO for receiving purchase orders
/// </summary>
public class ReceivePurchaseOrderDto
{
    [Required]
    public Guid PurchaseOrderId { get; set; }
    
    public string? DeliveryNote { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    
    [Required]
    public Guid ReceivedById { get; set; }
    
    public Guid? InspectedById { get; set; }
    public string? Notes { get; set; }
    
    [Required]
    public List<ReceivePurchaseOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for receiving individual purchase order items
/// </summary>
public class ReceivePurchaseOrderItemDto
{
    [Required]
    public Guid PurchaseOrderItemId { get; set; }
    
    [Required]
    public decimal ReceivedQuantity { get; set; }
    
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public Guid? LocationId { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
}

#endregion

#region Reorder DTOs

/// <summary>
/// Items requiring reorder information
/// </summary>
public class ReorderRequiredDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal AllocatedStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal RecommendedOrderQuantity { get; set; }
    public string? PrimarySupplier { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
}

#endregion