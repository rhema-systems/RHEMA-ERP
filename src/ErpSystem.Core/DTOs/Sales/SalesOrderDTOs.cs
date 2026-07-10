using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Sales;

#region Sales Order DTOs

/// <summary>
/// Sales Order summary for list views
/// </summary>
public class SalesOrderSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public SalesOrderType OrderType { get; set; }
    public SalesOrderStatus OrderStatus { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime? RequestedDeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public string? SalesRepName { get; set; }
    public string? OrderPriority { get; set; }
    public string ApprovalStatus { get; set; } = "Draft";
    public int LineCount { get; set; }
    public string? PropertyReference { get; set; }
    public PropertyType? PropertyType { get; set; }
    public SalesLinkedProjectUnitContextDto? ProjectUnitContext { get; set; }
}

/// <summary>
/// Detailed Sales Order with all fields and line items
/// </summary>
public class SalesOrderDetailDto : SalesOrderSummaryDto
{
    public DateTime? DueDate { get; set; }
    public DateTime? PromisedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public ShipmentMethod? ShipmentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    public string? BillingAddress { get; set; }
    public string? DeliveryInstructions { get; set; }
    public Guid? PaymentTermId { get; set; }
    public int PaymentTermsDays { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid? QuoteId { get; set; }
    public string? QuoteNumber { get; set; }
    public Guid? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid? SalesRepId { get; set; }
    public string? Terms { get; set; }
    public string? InternalNotes { get; set; }
    public string? ExternalNotes { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? TaxGroupId { get; set; }

    // Approval
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovalComments { get; set; }

    // Customer details
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }

    public List<SalesOrderLineDto> Lines { get; set; } = new();
    public List<SalesOrderStatusHistoryDto> StatusHistory { get; set; } = new();
    public List<DeliveryNoteSummaryDto> DeliveryNotes { get; set; } = new();
}

/// <summary>
/// Sales Order line item DTO
/// </summary>
public class SalesOrderLineDto
{
    public Guid Id { get; set; }
    public Guid SalesOrderId { get; set; }
    public int LineNumber { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TaxCode { get; set; }
    public string? Unit { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? TaxGroupId { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public bool IsStockReserved { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
}

/// <summary>
/// Status history entry DTO
/// </summary>
public class SalesOrderStatusHistoryDto
{
    public Guid Id { get; set; }
    public SalesOrderStatus? FromStatus { get; set; }
    public SalesOrderStatus ToStatus { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// DTO for creating a new Sales Order
/// </summary>
public class CreateSalesOrderDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    public SalesOrderType OrderType { get; set; } = SalesOrderType.Standard;

    public string? OrderPriority { get; set; } = "Normal";

    public Guid? SalesRepId { get; set; }

    // Dates
    public DateTime? RequestedDeliveryDate { get; set; }
    public DateTime? PromisedDeliveryDate { get; set; }

    // Financial
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? ShippingAmount { get; set; }
    public decimal? TaxAmount { get; set; }

    // Payment
    public Guid? PaymentTermId { get; set; }
    public string? Currency { get; set; } = "GHS";
    public decimal? ExchangeRate { get; set; }
    public Guid? TaxGroupId { get; set; }

    // Delivery
    public ShipmentMethod? ShipmentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    public string? BillingAddress { get; set; }
    public string? DeliveryInstructions { get; set; }
    public Guid? WarehouseId { get; set; }

    // Traceability
    public Guid? QuoteId { get; set; }
    public Guid? OpportunityId { get; set; }

    // TDC Property
    public string? PropertyReference { get; set; }
    public PropertyType? PropertyType { get; set; }

    // Notes
    public string? Terms { get; set; }
    public string? InternalNotes { get; set; }
    public string? ExternalNotes { get; set; }
    public string? ReferenceNumber { get; set; }

    [Required]
    public List<CreateSalesOrderLineDto> Lines { get; set; } = new();
}

/// <summary>
/// DTO for creating a Sales Order line item
/// </summary>
public class CreateSalesOrderLineDto
{
    public Guid? ProductId { get; set; }
    public Guid? InventoryItemId { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? ProductCode { get; set; }

    [Required]
    public decimal Quantity { get; set; } = 1;

    [Required]
    public decimal UnitPrice { get; set; }

    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxRate { get; set; }
    public string? TaxCode { get; set; }
    public string? Unit { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? GLAccountId { get; set; }
    public Guid? TaxGroupId { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating an existing Sales Order
/// </summary>
public class UpdateSalesOrderDto
{
    public string? OrderPriority { get; set; }
    public Guid? SalesRepId { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public DateTime? PromisedDeliveryDate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? ShippingAmount { get; set; }
    public Guid? PaymentTermId { get; set; }
    public ShipmentMethod? ShipmentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    public string? BillingAddress { get; set; }
    public string? DeliveryInstructions { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? PropertyReference { get; set; }
    public PropertyType? PropertyType { get; set; }
    public string? Terms { get; set; }
    public string? InternalNotes { get; set; }
    public string? ExternalNotes { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal? ExchangeRate { get; set; }

    public List<CreateSalesOrderLineDto>? Lines { get; set; }
}

/// <summary>
/// DTO for Sales Order approval action
/// </summary>
public class SalesOrderApprovalDto
{
    [Required]
    public bool Approved { get; set; }

    [JsonPropertyName("isApproved")]
    public bool IsApproved
    {
        get => Approved;
        set => Approved = value;
    }

    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// DTO for cancelling a Sales Order
/// </summary>
public class CancelSalesOrderDto
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}

#endregion
