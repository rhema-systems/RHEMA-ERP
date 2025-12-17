using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

#region Supplier DTOs

/// <summary>
/// Basic supplier information for lists and searches
/// </summary>
public class SupplierDto
{
    public Guid Id { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SupplierType { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? PrimaryContactName { get; set; }
    public string? PaymentTerms { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsActive { get; set; }
    public bool IsPreferred { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public DateTime? LastOrderDate { get; set; }
}

/// <summary>
/// Detailed supplier information including contacts and catalog
/// </summary>
public class SupplierDetailDto : SupplierDto
{
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? PrimaryContactTitle { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? TaxId { get; set; }
    public string? ShippingTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public string? Notes { get; set; }
    public List<SupplierContactDto> Contacts { get; set; } = new();
    public List<SupplierItemCatalogDto> ItemCatalog { get; set; } = new();
}

/// <summary>
/// DTO for creating or updating suppliers
/// </summary>
public class CreateSupplierDto
{
    [Required]
    public string SupplierCode { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string SupplierType { get; set; } = "Vendor";

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactTitle { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public string? PrimaryContactEmail { get; set; }

    public string? TaxId { get; set; }
    public string PaymentTerms { get; set; } = "Net 30";
    public string? ShippingTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public int LeadTimeDays { get; set; } = 7;

    public bool IsPreferred { get; set; } = false;
    public int? Rating { get; set; }
    public string? Notes { get; set; }

    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
}

/// <summary>
/// Supplier contact information
/// </summary>
public class SupplierContactDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
    public string ContactType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating supplier contacts
/// </summary>
public class CreateSupplierContactDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; } = false;
    public string ContactType { get; set; } = "General";
    public string? Notes { get; set; }
}

/// <summary>
/// Supplier item catalog information
/// </summary>
public class SupplierItemCatalogDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string? SupplierItemCode { get; set; }
    public string? SupplierItemName { get; set; }
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsActive { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
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
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? PromisedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public string? RequestedByName { get; set; }
}

/// <summary>
/// Detailed purchase order information
/// </summary>
public class PurchaseOrderDetailDto : PurchaseOrderSummaryDto
{
    public DateTime? ReceivedDate { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? ShippingTerms { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryInstructions { get; set; }
    public string? SupplierOrderNumber { get; set; }
    public string? ReferenceNumber { get; set; }

    // Supplier details
    public string? SupplierPhone { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierAddress { get; set; }

    public List<PurchaseOrderItemDto> Items { get; set; } = new();
    public List<PurchaseOrderReceiptDto> Receipts { get; set; } = new();
}

/// <summary>
/// Purchase order item information
/// </summary>
public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string? SupplierItemCode { get; set; }
    public string? ItemDescription { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating purchase orders
/// </summary>
public class CreatePurchaseOrderDto
{
    [Required]
    public Guid SupplierId { get; set; }

    public DateTime? RequiredDate { get; set; }
    public DateTime? PromisedDate { get; set; }

    public string? PaymentTerms { get; set; }
    public string? ShippingTerms { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }

    public Guid? DeliveryWarehouseId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryInstructions { get; set; }
    public string? ReferenceNumber { get; set; }

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
    public string? ItemDescription { get; set; }

    [Required]
    public decimal OrderedQuantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

#endregion

#region Purchase Order Receipt DTOs

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
    public bool RequiresInspection { get; set; }
    public DateTime? InspectionDate { get; set; }
    public string? InspectionResult { get; set; }
    public string? InspectionNotes { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public List<PurchaseOrderReceiptItemDto> Items { get; set; } = new();
}

/// <summary>
/// Purchase order receipt item information
/// </summary>
public class PurchaseOrderReceiptItemDto
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public Guid? LocationId { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
    public string? QualityStatus { get; set; }
    public string? QualityNotes { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
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
    public bool RequiresInspection { get; set; } = false;

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
    public string? QualityStatus { get; set; }
    public string? QualityNotes { get; set; }
}

#endregion

#region Purchase Requisition DTOs

/// <summary>
/// Purchase requisition summary for lists
/// </summary>
public class PurchaseRequisitionSummaryDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public DateTime RequisitionDate { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime? RequiredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Department { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>
/// Detailed purchase requisition information
/// </summary>
public class PurchaseRequisitionDetailDto : PurchaseRequisitionSummaryDto
{
    public string? CostCenter { get; set; }
    public string? Justification { get; set; }
    public string? Notes { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public List<PurchaseRequisitionItemDto> Items { get; set; } = new();
}

/// <summary>
/// Purchase requisition item information
/// </summary>
public class PurchaseRequisitionItemDto
{
    public Guid Id { get; set; }
    public Guid RequisitionId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal EstimatedUnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime? RequiredDate { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public string? Notes { get; set; }
    public string? Specifications { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
}

/// <summary>
/// DTO for creating purchase requisitions
/// </summary>
public class CreatePurchaseRequisitionDto
{
    public DateTime? RequiredDate { get; set; }
    public string Priority { get; set; } = "Normal";
    public string? Department { get; set; }
    public string? CostCenter { get; set; }
    public string? Justification { get; set; }
    public string? Notes { get; set; }

    [Required]
    public Guid RequestedById { get; set; }

    [Required]
    public List<CreatePurchaseRequisitionItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for creating purchase requisition items
/// </summary>
public class CreatePurchaseRequisitionItemDto
{
    public Guid? InventoryItemId { get; set; }

    [Required]
    public string ItemDescription { get; set; } = string.Empty;

    [Required]
    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "EA";
    public decimal EstimatedUnitPrice { get; set; } = 0;
    public DateTime? RequiredDate { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? Notes { get; set; }
    public string? Specifications { get; set; }
}

#endregion

#region Common DTOs

/// <summary>
/// DTO for updating status
/// </summary>
public class UpdateStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

/// <summary>
/// DTO for approval actions
/// </summary>
public class ApprovalDto
{
    [Required]
    public bool Approved { get; set; }

    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }
}

#endregion
