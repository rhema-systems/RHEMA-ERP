using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

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
    public string OrderType { get; set; } = "Standard";
    public Guid SupplierId { get; set; } // Keep for backward compatibility, maps to BusinessPartnerId
    public string SupplierName { get; set; } = string.Empty; // Keep for backward compatibility, maps to BusinessPartner.PartnerName
    public DateTime OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? PromisedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string? RequestedByName { get; set; }
    public ProcurementPurchaseOrderSourceType? ProcurementSourceType { get; set; }
    public ProcurementCategoryClass? ProcurementCategory { get; set; }
    public string? ProcurementSourceReference { get; set; }
    /// <summary>
    /// Runtime workflow info (populated when status is workflow-driven)
    /// </summary>
    public string? CurrentWorkflowStepName { get; set; }
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
    public decimal MiscellaneousCost { get; set; }
    public decimal TotalAdditionalCost { get; set; }
    public string CostAllocationMethod { get; set; } = "SpreadToItemCost";
    public string CostApportionmentBasis { get; set; } = "Value";
    public string? ExpenseGLAccount { get; set; }
    public bool CostsAllocated { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? ShippingTerms { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryInstructions { get; set; }
    public string? SupplierOrderNumber { get; set; } // Keep for backward compatibility, maps to BusinessPartnerOrderNumber
    public string? ReferenceNumber { get; set; }

    // Business Partner details (keeping Supplier names for backward compatibility)
    public string? SupplierPhone { get; set; }
    public string? SupplierEmail { get; set; }
    public string? SupplierAddress { get; set; }

    // Immutable approved source lineage
    public Guid? ProcurementSourceId { get; set; }
    public Guid? SourceRequisitionId { get; set; }
    public string? SourceRequisitionNumber { get; set; }
    public Guid? SourcingReleaseId { get; set; }
    public Guid? SourcingCaseId { get; set; }
    public Guid? AwardReadinessDecisionId { get; set; }
    public string? SourceIntegrityHash { get; set; }
    public DateTime? SourceValidatedAtUtc { get; set; }

    // Tender/Contract Integration
    public Guid? TenderAwardId { get; set; }
    public string? TenderNumber { get; set; }
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public bool IsFromTender { get; set; }
    public bool IsFromContract { get; set; }
    
    // Contract Utilization (if applicable)
    public decimal? ContractValue { get; set; }
    public decimal? ContractUsedValue { get; set; }
    public decimal? ContractRemainingValue { get; set; }
    public decimal? ContractUtilizationPercent { get; set; }

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
    public string? SupplierItemCode { get; set; } // Keep for backward compatibility, maps to BusinessPartnerItemCode
    public string? ItemDescription { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public Guid? ItemUnitOfMeasureId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal AllocatedAdditionalCost { get; set; }
    public decimal AllocatedCostPerUnit { get; set; }
    public decimal LandedUnitCost { get; set; }
    public Guid? PriceListLineId { get; set; }
    public string? PriceListName { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    
    // UOM conversion info
    public decimal? BaseUnitConversionFactor { get; set; }
    public string? BaseUnitOfMeasure { get; set; }
}

/// <summary>
/// DTO for creating purchase orders
/// </summary>
public class CreatePurchaseOrderDto
{
    [Required]
    public ProcurementPurchaseOrderSourceType? SourceType { get; set; }

    [Required]
    public Guid? SourceId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    /// <summary>
    /// Purchase order type (e.g. Standard, Consignment, DropShip).
    /// </summary>
    public string? OrderType { get; set; }

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

    public decimal? TaxAmount { get; set; }
    public decimal? ShippingCost { get; set; }
    public decimal? MiscellaneousCost { get; set; }
    public string? CostAllocationMethod { get; set; }
    public string? CostApportionmentBasis { get; set; }
    public string? ExpenseGLAccount { get; set; }
    public decimal? DiscountAmount { get; set; }

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
    public string UnitOfMeasure { get; set; } = "EA";

    public Guid? ItemUnitOfMeasureId { get; set; }
    
    public Guid? WarehouseId { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    public Guid? PriceListLineId { get; set; }

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
    public decimal ReceiptTolerancePercent { get; set; }
    public string? ReceiptSourceIntegrityHash { get; set; }
    public DateTime? ReceiptSourceValidatedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
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
    public string? UnitOfMeasure { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }
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

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

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
    public Guid? WarehouseId { get; set; }
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
    public string? SourcePlanNumber { get; set; }
    public string? SourcePlanItemDescription { get; set; }
    public string? BudgetCode { get; set; }
    public ProcurementCategoryClass? ProcurementCategory { get; set; }
    public string? ProjectCode { get; set; }
    public PurchaseRequisitionType RequisitionType { get; set; }
    public string? SpecificationTemplateReference { get; set; }
    public string? ApprovedExceptionReference { get; set; }

    // Workflow display helpers (optional)
    public string? CurrentWorkflowStepName { get; set; }
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
    public PurchaseRequisitionLinkageDto Linkage { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
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
    public Guid? PreferredSupplierId { get; set; } // Keep for backward compatibility, maps to PreferredBusinessPartnerId
    public string? PreferredSupplierName { get; set; } // Keep for backward compatibility, maps to PreferredBusinessPartner.PartnerName
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

    /// <summary>
    /// Authoritative HR department selected by the requester.  Department is
    /// retained as the display snapshot for legacy reporting only.
    /// </summary>
    public Guid? DepartmentId { get; set; }
    public string? CostCenter { get; set; }
    public string? Justification { get; set; }
    public string? Notes { get; set; }
    public SavePurchaseRequisitionLinkageRequest Linkage { get; set; } = new();

    [Required]
    public Guid RequestedById { get; set; }

    [Required]
    public List<CreatePurchaseRequisitionItemDto> Items { get; set; } = new();
}

public sealed class UpdatePurchaseRequisitionDto : CreatePurchaseRequisitionDto
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SavePurchaseRequisitionLinkageRequest
{
    public Guid? SourcePlanItemId { get; set; }
    public Guid? BudgetId { get; set; }
    public ProcurementCategoryClass? ProcurementCategory { get; set; }

    [StringLength(100)]
    public string? CostCenter { get; set; }

    public Guid? ProjectId { get; set; }
    public PurchaseRequisitionType RequisitionType { get; set; } = PurchaseRequisitionType.StockReplenishment;
    public Guid? SpecificationTemplateId { get; set; }
    public Guid? ApprovedExceptionRuleId { get; set; }
    public Guid? ExceptionWorkflowInstanceId { get; set; }

    [StringLength(200)]
    public string? ExceptionApprovalReference { get; set; }

    [StringLength(500)]
    public string? ExceptionEvidenceReference { get; set; }
}

public sealed class PurchaseRequisitionLinkageDto
{
    public Guid? SourcePlanId { get; set; }
    public Guid? SourcePlanItemId { get; set; }
    public string? SourcePlanNumber { get; set; }
    public string? SourcePlanTitle { get; set; }
    public string? SourcePlanItemDescription { get; set; }
    public Guid? BudgetId { get; set; }
    public string? BudgetCode { get; set; }
    public decimal? BudgetAllocated { get; set; }
    public decimal? BudgetRemaining { get; set; }
    public ProcurementCategoryClass? ProcurementCategory { get; set; }
    public string? CostCenter { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectName { get; set; }
    public PurchaseRequisitionType RequisitionType { get; set; }
    public Guid? SpecificationTemplateId { get; set; }
    public string? SpecificationTemplateCode { get; set; }
    public string? SpecificationTemplateName { get; set; }
    public int? SpecificationTemplateVersion { get; set; }
    public Guid? ApprovedExceptionRuleId { get; set; }
    public string? ApprovedExceptionRuleCode { get; set; }
    public string? ApprovedExceptionName { get; set; }
    public Guid? ExceptionWorkflowInstanceId { get; set; }
    public string? ExceptionApprovalReference { get; set; }
    public string? ExceptionEvidenceReference { get; set; }
    public Guid? ExceptionApprovedById { get; set; }
    public string? ExceptionApprovedByName { get; set; }
    public DateTime? ExceptionApprovedAtUtc { get; set; }
    public int Revision { get; set; }
    public DateTime? LastUpdatedAtUtc { get; set; }
    public Guid? LastUpdatedById { get; set; }
    public string? LastUpdatedByName { get; set; }
}

public sealed class PurchaseRequisitionLinkageOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Status { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? LinkedBudgetId { get; set; }
    public string? ParentReference { get; set; }
    public string? Category { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
}

public sealed class PurchaseRequisitionNamedOptionDto
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class PurchaseRequisitionLinkageOptionsDto
{
    public List<PurchaseRequisitionLinkageOptionDto> PlanItems { get; set; } = new();
    public List<PurchaseRequisitionLinkageOptionDto> Budgets { get; set; } = new();
    public List<PurchaseRequisitionLinkageOptionDto> Projects { get; set; } = new();
    public List<PurchaseRequisitionLinkageOptionDto> SpecificationTemplates { get; set; } = new();
    public List<PurchaseRequisitionLinkageOptionDto> ApprovedExceptionRules { get; set; } = new();
    public List<PurchaseRequisitionLinkageOptionDto> ApprovedExceptionWorkflows { get; set; } = new();
    public List<PurchaseRequisitionNamedOptionDto> Categories { get; set; } = new();
    public List<PurchaseRequisitionNamedOptionDto> RequestTypes { get; set; } = new();
    public List<string> CostCenters { get; set; } = new();
}

public sealed class PurchaseRequisitionExportDto
{
    public string SchemaVersion { get; set; } = "tdc.pr-linkage.v1";
    public DateTime ExportedAtUtc { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public DateTime RequisitionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public decimal TotalAmount { get; set; }
    public PurchaseRequisitionLinkageDto Linkage { get; set; } = new();
    public List<PurchaseRequisitionItemDto> Items { get; set; } = new();
}

public sealed class PurchaseRequisitionLinkageHistoryDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class PurchaseRequisitionSubmissionReadinessDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public bool CanSubmit { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Basis { get; set; }
    public Guid? SourcePlanId { get; set; }
    public Guid? SourcePlanItemId { get; set; }
    public string? SourcePlanNumber { get; set; }
    public string? SourcePlanItemDescription { get; set; }
    public Guid? AppSubmissionId { get; set; }
    public string? AppSubmissionNumber { get; set; }
    public int? AppSubmissionAttemptNumber { get; set; }
    public string? AppSubmissionStatus { get; set; }
    public string? AppAcknowledgementReference { get; set; }
    public DateTime? AppAcknowledgedAtUtc { get; set; }
    public Guid? ApprovedExceptionRuleId { get; set; }
    public string? ApprovedExceptionRuleCode { get; set; }
    public Guid? ExceptionWorkflowInstanceId { get; set; }
    public string? ExceptionApprovalReference { get; set; }
    public string? ExceptionEvidenceReference { get; set; }
    public DateTime? ExceptionApprovedAtUtc { get; set; }
    public List<string> RequiredActions { get; set; } = new();
}

public sealed class PurchaseRequisitionSubmissionControlHistoryDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? RuleCode { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class PurchaseRequisitionBudgetReadinessDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public bool CanReserve { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Basis { get; set; }
    public Guid? BudgetId { get; set; }
    public string? BudgetCode { get; set; }
    public string? BudgetStatus { get; set; }
    public string? Currency { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UtilizedAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal ReservedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal ShortfallAmount { get; set; }
    public Guid? CommitmentId { get; set; }
    public string? CommitmentReference { get; set; }
    public string? CommitmentStatus { get; set; }
    public int? ReservationSequence { get; set; }
    public DateTime? ReservedAtUtc { get; set; }
    public bool IsOverride { get; set; }
    public Guid? OverrideRuleId { get; set; }
    public string? OverrideRuleCode { get; set; }
    public Guid? OverrideWorkflowInstanceId { get; set; }
    public string? OverrideApprovalReference { get; set; }
    public string? OverrideEvidenceReference { get; set; }
    public DateTime? OverrideApprovedAtUtc { get; set; }
    public List<string> RequiredActions { get; set; } = new();
}

public sealed class PurchaseRequisitionBudgetReleaseDto
{
    public Guid RequisitionId { get; set; }
    public bool Released { get; set; }
    public Guid? CommitmentId { get; set; }
    public string? CommitmentReference { get; set; }
    public decimal ReleasedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class PurchaseRequisitionBudgetControlHistoryDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? RuleCode { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class PurchaseRequisitionAuthorityReadinessDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public bool CanSubmit { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ProcurementCategoryClass? Category { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid? PolicySetId { get; set; }
    public string? PolicyCode { get; set; }
    public string? PolicyName { get; set; }
    public int? PolicyVersion { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public string? WorkflowName { get; set; }
    public int? WorkflowVersion { get; set; }
    public Guid? AuthorityRouteId { get; set; }
    public string? RouteReference { get; set; }
    public int? AttemptNumber { get; set; }
    public DateTime? CapturedAtUtc { get; set; }
    public string? IntegrityHash { get; set; }
    public string? CurrentWorkflowStage { get; set; }
    public string? CurrentWorkflowStageStatus { get; set; }
    public List<PurchaseRequisitionAuthorityStepDto> Steps { get; set; } = new();
    public List<ProcurementComplianceFindingDto> Findings { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
}

public sealed class PurchaseRequisitionAuthorityStepDto
{
    public int Sequence { get; set; }
    public Guid RuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RulePolicyCode { get; set; } = string.Empty;
    public int RulePolicyVersion { get; set; }
    public string SourceDecisionKey { get; set; } = string.Empty;
    public string AuthorityName { get; set; } = string.Empty;
    public string AuthorityRole { get; set; } = string.Empty;
    public int Quorum { get; set; }
    public bool IsObserver { get; set; }
    public string? EscalationAuthority { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; }
    public bool UpperInclusive { get; set; }
    public Guid WorkflowStepId { get; set; }
    public string WorkflowStepName { get; set; } = string.Empty;
    public int WorkflowStepOrder { get; set; }
}

public sealed class PurchaseRequisitionAuthorityRouteHistoryDto
{
    public Guid Id { get; set; }
    public string RouteReference { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public int WorkflowVersion { get; set; }
    public ProcurementCategoryClass Category { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
    public string CapturedByName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<PurchaseRequisitionAuthorityStepDto> Steps { get; set; } = new();
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
    public Guid? PreferredSupplierId { get; set; } // Keep for backward compatibility, maps to PreferredBusinessPartnerId
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
