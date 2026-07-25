using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Sales;

#region Delivery Note DTOs

/// <summary>
/// Delivery Note summary for list views
/// </summary>
public class DeliveryNoteSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public DeliveryNoteStatus DeliveryStatus { get; set; }
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public ShipmentMethod ShipmentMethod { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public int LineCount { get; set; }
    public string? WarehouseName { get; set; }
}

/// <summary>
/// Detailed Delivery Note with all fields and line items
/// </summary>
public class DeliveryNoteDetailDto : DeliveryNoteSummaryDto
{
    public string? ShippingAddress { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? PackedById { get; set; }
    public string? PackedByName { get; set; }
    public Guid? ShippedById { get; set; }
    public string? ShippedByName { get; set; }
    public Guid? ReceivedById { get; set; }
    public string? ReceivedByName { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverSignaturePath { get; set; }
    public string? DeliveryNotes { get; set; }
    public string? InternalNotes { get; set; }
    public string? ExternalNotes { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; }
    public Guid? TaxGroupId { get; set; }

    public List<DeliveryNoteLineDto> Lines { get; set; } = new();
}

/// <summary>
/// Delivery Note line item DTO
/// </summary>
public class DeliveryNoteLineDto
{
    public Guid Id { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public int LineNumber { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal DamagedQuantity { get; set; }
    public string? Unit { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid? LocationId { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public bool IsStockDeducted { get; set; }
    public string? Notes { get; set; }
    public string? DamageNotes { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid? TaxGroupId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
}

/// <summary>
/// DTO for creating a new Delivery Note from a Sales Order
/// </summary>
public class CreateDeliveryNoteDto
{
    [Required]
    public Guid SalesOrderId { get; set; }

    public ShipmentMethod ShipmentMethod { get; set; } = Enums.ShipmentMethod.CompanyDelivery;

    public string? ShippingAddress { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? DeliveryNotes { get; set; }
    public string? InternalNotes { get; set; }
    public string? ExternalNotes { get; set; }

    [Required]
    public List<CreateDeliveryNoteLineDto> Lines { get; set; } = new();
}

/// <summary>
/// DTO for creating a Delivery Note line item
/// </summary>
public class CreateDeliveryNoteLineDto
{
    [Required]
    public Guid SalesOrderLineId { get; set; }

    public Guid? InventoryItemId { get; set; }

    [Required]
    public decimal DispatchedQuantity { get; set; }

    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for confirming delivery (receiver confirmation)
/// </summary>
public class ConfirmDeliveryDto
{
    [Required]
    public string ReceiverName { get; set; } = string.Empty;

    public string? ReceiverSignaturePath { get; set; }
    public string? DeliveryNotes { get; set; }

    /// <summary>
    /// Optional: per-line delivery quantities (for partial deliveries)
    /// </summary>
    public List<ConfirmDeliveryLineDto>? Lines { get; set; }
}

/// <summary>
/// Per-line delivery confirmation
/// </summary>
public class ConfirmDeliveryLineDto
{
    [Required]
    public Guid DeliveryNoteLineId { get; set; }

    [Required]
    public decimal DeliveredQuantity { get; set; }

    public decimal? DamagedQuantity { get; set; }
    public string? DamageNotes { get; set; }
}

#endregion
