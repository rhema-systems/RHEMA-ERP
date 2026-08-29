using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities.Procurement;

#region Supplier Management

/// <summary>
/// Supplier/Vendor information for procurement
/// </summary>
public class Supplier : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string SupplierCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string SupplierType { get; set; } = "Vendor"; // Vendor, Manufacturer, Distributor

    // Contact Information
    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? ZipCode { get; set; }

    [MaxLength(50)]
    public string? Country { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    // Primary Contact
    [MaxLength(100)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactTitle { get; set; }

    [MaxLength(50)]
    public string? PrimaryContactPhone { get; set; }

    [MaxLength(100)]
    public string? PrimaryContactEmail { get; set; }

    // Business Information
    [MaxLength(50)]
    public string? TaxId { get; set; }

    public bool IsWithholdingTaxApplicable { get; set; }

    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;

    [MaxLength(100)]
    public string? PaymentTerms { get; set; } = "Net 30";

    [MaxLength(100)]
    public string? ShippingTerms { get; set; }

    public decimal? CreditLimit { get; set; }

    public int LeadTimeDays { get; set; } = 7;

    // Status and Rating
    public bool IsActive { get; set; } = true;
    public bool IsPreferred { get; set; } = false;

    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Inactive, Blacklisted

    public int? Rating { get; set; } // 1-5 rating

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Blacklist Information
    public bool IsBlacklisted { get; set; } = false;

    [MaxLength(1000)]
    public string? BlacklistReason { get; set; }

    public DateTime? BlacklistDate { get; set; }
    public DateTime? BlacklistExpiryDate { get; set; }

    // Dates
    public DateTime? LastOrderDate { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    // Finance/GL Integration
    public Guid? DefaultApAccountId { get; set; }
    public Guid? DefaultArAccountId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public Guid? PaymentTermId { get; set; }
    public virtual ErpSystem.Core.Entities.Finance.PaymentTerm? PaymentTerm { get; set; }

    // Navigation Properties
    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public virtual ICollection<SupplierContact> Contacts { get; set; } = new List<SupplierContact>();
    public virtual ICollection<SupplierItemCatalog> ItemCatalogs { get; set; } = new List<SupplierItemCatalog>();
}

/// <summary>
/// Additional supplier contacts
/// </summary>
public class SupplierContact : TenantEntity
{
    [Required]
    public Guid SupplierId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Title { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsPrimary { get; set; } = false;

    [MaxLength(50)]
    public string ContactType { get; set; } = "General"; // General, Sales, Support, Billing

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Supplier Supplier { get; set; } = null!;
}

/// <summary>
/// Supplier item catalog with pricing
/// </summary>
public class SupplierItemCatalog : TenantEntity
{
    [Required]
    public Guid SupplierId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    [MaxLength(200)]
    public string? SupplierItemName { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; } = 0;

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    public decimal MinimumOrderQuantity { get; set; } = 1;

    public int LeadTimeDays { get; set; } = 7;

    public bool IsPreferred { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Supplier Supplier { get; set; } = null!;
    public virtual InventoryItem? InventoryItem { get; set; }
}

#endregion

#region Purchase Orders

/// <summary>
/// Purchase orders for procurement
/// </summary>
public class PurchaseOrder : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    public Guid BusinessPartnerId { get; set; }

    // Dates
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredDate { get; set; }
    public DateTime? PromisedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }

    // Status and Approval
    [MaxLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, Approved, Sent, Acknowledged, PartiallyReceived, Received, Cancelled

    public Guid? RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    // Financial
    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MiscellaneousCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAdditionalCost { get; set; } = 0; // ShippingCost + MiscellaneousCost

    /// <summary>
    /// How shipping and similar costs are handled:
    /// - SpreadToItemCost: allocate proportionally to line items
    /// - GLExpense: post directly to expense account
    /// </summary>
    [MaxLength(50)]
    public string CostAllocationMethod { get; set; } = "SpreadToItemCost";

    /// <summary>
    /// Basis used when spreading costs to items.
    /// Supported values: Value, Weight, Quantity.
    /// </summary>
    [MaxLength(50)]
    public string CostApportionmentBasis { get; set; } = "Value";

    /// <summary>
    /// GL account used when CostAllocationMethod is GLExpense.
    /// </summary>
    [MaxLength(50)]
    public string? ExpenseGLAccount { get; set; }

    public bool CostsAllocated { get; set; } = false;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; } = 0;

    // Terms and Conditions
    [MaxLength(100)]
    public string? PaymentTerms { get; set; }

    [MaxLength(100)]
    public string? ShippingTerms { get; set; }

    [MaxLength(2000)]
    public string? Terms { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Delivery Information
    public Guid? DeliveryWarehouseId { get; set; }

    [MaxLength(500)]
    public string? DeliveryAddress { get; set; }

    [MaxLength(2000)]
    public string? DeliveryInstructions { get; set; }

    // Reference Information
    [MaxLength(100)]
    public string? BusinessPartnerOrderNumber { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    // === ENHANCED FIELDS ===

    // PO Type
    [MaxLength(50)]
    public string OrderType { get; set; } = "Standard"; // Standard, Blanket, Contract, DropShip, Consignment

    // For Blanket/Contract POs
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ContractValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ContractUsedValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ContractRemainingValue { get; set; }

    // Source PR Reference
    public Guid? SourceRequisitionId { get; set; }

    [MaxLength(50)]
    public string? SourceRequisitionNumber { get; set; }

    // Mandatory governed source lineage (TDC-0403)
    public ProcurementPurchaseOrderSourceType? ProcurementSourceType { get; set; }
    public Guid? ProcurementSourceId { get; set; }

    [MaxLength(100)]
    public string? ProcurementSourceReference { get; set; }

    public Guid? SourcingReleaseId { get; set; }
    public Guid? SourcingCaseId { get; set; }
    public Guid? AwardReadinessDecisionId { get; set; }

    /// <summary>
    /// Immutable category snapshot inherited from the approved requisition.
    /// It selects the Goods receipt, Services completion, or Works certificate
    /// acceptance owner without relying on descriptions or inventory mapping.
    /// </summary>
    public ProcurementCategoryClass? ProcurementCategory { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? SourceSnapshotJson { get; set; }

    [MaxLength(64)]
    public string? SourceIntegrityHash { get; set; }

    public DateTime? SourceValidatedAtUtc { get; set; }

    // Source RFQ Reference (when PO is created from an RFQ award)
    public Guid? SourceRfqId { get; set; }

    [MaxLength(50)]
    public string? SourceRfqNumber { get; set; }

    /// <summary>
    /// Award mode that generated the PO: WinnerTakesAll or SplitAward.
    /// </summary>
    [MaxLength(30)]
    public string? SourceRfqAwardType { get; set; }

    /// <summary>
    /// For winner-takes-all awards, this is the selected quote.
    /// For split-award, individual items may reference different quotes.
    /// </summary>
    public Guid? SourceRfqQuoteId { get; set; }

    // Tender/Contract Integration
    /// <summary>
    /// Reference to the tender award that generated this PO
    /// </summary>
    public Guid? TenderAwardId { get; set; }

    /// <summary>
    /// Reference to the contract this PO is linked to
    /// </summary>
    public Guid? ContractId { get; set; }

    [MaxLength(50)]
    public string? TenderNumber { get; set; }

    [MaxLength(50)]
    public string? ContractNumber { get; set; }

    // Currency
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1;

    // Budget Tracking
    public Guid? BudgetId { get; set; }

    [MaxLength(100)]
    public string? BudgetCode { get; set; }

    public bool BudgetValidated { get; set; } = false;

    // Revision/Amendment
    public int RevisionNumber { get; set; } = 0;
    public DateTime? LastAmendedAt { get; set; }
    public Guid? LastAmendedById { get; set; }

    // Auto-Close Settings
    public bool AutoCloseOnReceipt { get; set; } = true;

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TolerancePercent { get; set; } = 5; // +/- tolerance for receipt

    // === END ENHANCED FIELDS ===

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? RequestedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? LastAmendedBy { get; set; }
    public virtual PurchaseRequisition? SourceRequisition { get; set; }
    public virtual TenderAward? TenderAward { get; set; }
    // Note: Warehouse navigation would be added if cross-module references are allowed
    // Note: Contract navigation would be added when Contract entity is available
    public virtual ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    public virtual ICollection<PurchaseOrderReceipt> Receipts { get; set; } = new List<PurchaseOrderReceipt>();
}

/// <summary>
/// Individual items on a purchase order
/// </summary>
public class PurchaseOrderItem : TenantEntity
{
    [Required]
    public Guid PurchaseOrderId { get; set; }

    /// <summary>
    /// Reference to inventory item. Can be null for items from tenders that don't have inventory mapping yet.
    /// </summary>
    public Guid? InventoryItemId { get; set; }

    [MaxLength(100)]
    public string? BusinessPartnerItemCode { get; set; }

    [MaxLength(200)]
    public string? ItemDescription { get; set; }

    [Required]
    public decimal OrderedQuantity { get; set; } = 1;

    public decimal ReceivedQuantity { get; set; } = 0;
    public decimal RemainingQuantity { get; set; } = 0;

    /// <summary>
    /// Unit of measure for this line item
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Reference to the specific UOM from the item's UOM schedule (optional)
    /// </summary>
    public Guid? ItemUnitOfMeasureId { get; set; }

    /// <summary>
    /// Warehouse where this item should be delivered/received
    /// </summary>
    public Guid? WarehouseId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; } = 0;

    /// <summary>
    /// Additional landed cost allocated to this line from shipping/misc costs.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAdditionalCost { get; set; } = 0;

    /// <summary>
    /// Allocated additional cost per unit on the line.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal AllocatedCostPerUnit { get; set; } = 0;

    /// <summary>
    /// Effective unit cost after allocated landed costs.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedUnitCost { get; set; } = 0;

    // RFQ traceability (optional)
    public Guid? SourceRfqItemId { get; set; }
    public Guid? SourceRfqQuoteId { get; set; }
    public Guid? SourceRfqQuoteItemId { get; set; }

    /// <summary>
    /// Reference to the price list line used for this item (optional)
    /// </summary>
    public Guid? PriceListLineId { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual InventoryItem? InventoryItem { get; set; }
    public virtual ItemUnitOfMeasure? ItemUnitOfMeasure { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
}

#endregion

#region Purchase Order Receipts

/// <summary>
/// Purchase order receipts/deliveries
/// </summary>
public class PurchaseOrderReceipt : TenantEntity
{
    [Required]
    public Guid PurchaseOrderId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? DeliveryNote { get; set; }

    [MaxLength(100)]
    public string? CarrierName { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    public Guid? ReceivedById { get; set; }
    public Guid? InspectedById { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Received"; // Received, Inspected, Accepted, Rejected

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Governed source/capacity snapshot (TDC-0501)
    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ReceiptTolerancePercent { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ReceiptSourceSnapshotJson { get; set; }

    [MaxLength(64)]
    public string? ReceiptSourceIntegrityHash { get; set; }

    public DateTime? ReceiptSourceValidatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Quality Control
    public bool RequiresInspection { get; set; } = false;
    public DateTime? InspectionDate { get; set; }

    [MaxLength(50)]
    public string? InspectionResult { get; set; } // Passed, Failed, Conditional

    [MaxLength(2000)]
    public string? InspectionNotes { get; set; }

    // Navigation Properties
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual ApplicationUser? ReceivedBy { get; set; }
    public virtual ApplicationUser? InspectedBy { get; set; }
    public virtual ICollection<PurchaseOrderReceiptItem> Items { get; set; } = new List<PurchaseOrderReceiptItem>();
}

/// <summary>
/// Items received in a specific receipt
/// </summary>
public class PurchaseOrderReceiptItem : TenantEntity
{
    [Required]
    public Guid ReceiptId { get; set; }
  
    [Required]
    public Guid PurchaseOrderItemId { get; set; }
  
    public decimal ReceivedQuantity { get; set; } = 0;
    public decimal AcceptedQuantity { get; set; } = 0;
    public decimal RejectedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal OrderedQuantitySnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PreviouslyReceiptedQuantitySnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ToleranceQuantitySnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal MaximumReceivableQuantitySnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RemainingQuantityBeforeReceiptSnapshot { get; set; }

    [MaxLength(64)]
    public string? ReceiptLineIntegrityHash { get; set; }

    /// <summary>
    /// Snapshot of the PO line UOM at the time of receipt (audit/history-safe).
    /// </summary>
    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    /// <summary>
    /// Snapshot of the PO line's ItemUnitOfMeasureId at the time of receipt (optional).
    /// </summary>
    public Guid? ItemUnitOfMeasureId { get; set; }

    public Guid? LocationId { get; set; } // Where it was put away
  
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Quality Control
    [MaxLength(50)]
    public string? QualityStatus { get; set; } // Passed, Failed, Pending

    [MaxLength(1000)]
    public string? QualityNotes { get; set; }

    // Navigation Properties
    public virtual PurchaseOrderReceipt Receipt { get; set; } = null!;
    public virtual PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    // Note: WarehouseLocation navigation would be added if cross-module references are allowed
}

#endregion

#region Purchase Requisitions

/// <summary>
/// Purchase requisitions - internal requests for purchasing
/// </summary>
public class PurchaseRequisition : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RequisitionNumber { get; set; } = string.Empty;

    public DateTime RequisitionDate { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid RequestedById { get; set; }

    public DateTime? RequiredDate { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved, Rejected, Ordered

    [MaxLength(50)]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(100)]
    public string? CostCenter { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // === ENHANCED FIELDS ===

    // Requisition Type
    public PurchaseRequisitionType RequisitionType { get; set; } = PurchaseRequisitionType.StockReplenishment;

    // Budget Reference
    public Guid? BudgetId { get; set; }

    [MaxLength(100)]
    public string? BudgetCode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetAllocated { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetRemaining { get; set; }

    public bool BudgetValidated { get; set; } = false;

    // Project Reference (for project purchases)
    public Guid? ProjectId { get; set; }

    [MaxLength(100)]
    public string? ProjectCode { get; set; }

    [MaxLength(200)]
    public string? ProjectName { get; set; }

    // Delivery Information
    public Guid? DeliveryWarehouseId { get; set; }

    [MaxLength(500)]
    public string? DeliveryAddress { get; set; }

    [MaxLength(500)]
    public string? DeliveryInstructions { get; set; }

    // Auto-Generated Flag (from planning module)
    public bool IsAutoGenerated { get; set; } = false;

    [MaxLength(100)]
    public string? GeneratedFrom { get; set; } // Planning, ReorderAlert, etc.

    public Guid? SourcePlanId { get; set; }

    public Guid? SourcePlanItemId { get; set; }

    [MaxLength(50)]
    public string? SourcePlanNumber { get; set; }

    [MaxLength(200)]
    public string? SourcePlanTitle { get; set; }

    [MaxLength(200)]
    public string? SourcePlanItemDescription { get; set; }

    public ProcurementCategoryClass? ProcurementCategory { get; set; }

    public Guid? SpecificationTemplateId { get; set; }

    [MaxLength(50)]
    public string? SpecificationTemplateCode { get; set; }

    [MaxLength(200)]
    public string? SpecificationTemplateName { get; set; }

    public int? SpecificationTemplateVersion { get; set; }

    public Guid? ApprovedExceptionRuleId { get; set; }

    [MaxLength(50)]
    public string? ApprovedExceptionRuleCode { get; set; }

    [MaxLength(200)]
    public string? ApprovedExceptionName { get; set; }

    public Guid? ExceptionWorkflowInstanceId { get; set; }

    [MaxLength(200)]
    public string? ExceptionApprovalReference { get; set; }

    [MaxLength(500)]
    public string? ExceptionEvidenceReference { get; set; }

    public Guid? ExceptionApprovedById { get; set; }

    [MaxLength(300)]
    public string? ExceptionApprovedByName { get; set; }

    public DateTime? ExceptionApprovedAtUtc { get; set; }

    public int LinkageRevision { get; set; }

    public DateTime? LinkageLastUpdatedAtUtc { get; set; }

    public Guid? LinkageLastUpdatedById { get; set; }

    [MaxLength(300)]
    public string? LinkageLastUpdatedByName { get; set; }

    // Multi-Level Approval Support
    public int ApprovalLevel { get; set; } = 0; // Current approval level
    public int RequiredApprovalLevel { get; set; } = 1; // Required approval level based on amount

    public Guid? CurrentApproverId { get; set; }

    [MaxLength(2000)]
    public string? ApprovalHistory { get; set; } // JSON array of approval steps

    // Amendment Tracking
    public int RevisionNumber { get; set; } = 0;
    public DateTime? LastAmendedAt { get; set; }
    public Guid? LastAmendedById { get; set; }

    [MaxLength(2000)]
    public string? AmendmentNotes { get; set; }

    // Currency
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    // Preferred Business Partner (if known at PR stage)
    public Guid? PreferredBusinessPartnerId { get; set; }

    // === END ENHANCED FIELDS ===

    // Approval
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(2000)]
    public string? RejectionReason { get; set; }

    // Total Amount
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; } = 0;

    // Navigation Properties
    public virtual ApplicationUser RequestedBy { get; set; } = null!;
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? CurrentApprover { get; set; }
    public virtual ApplicationUser? LastAmendedBy { get; set; }
    public virtual ProcurementPlan? SourcePlan { get; set; }
    public virtual ProcurementPlanItem? SourcePlanItem { get; set; }
    public virtual ProcurementBudget? Budget { get; set; }
    public virtual ProcurementSpecificationTemplate? SpecificationTemplate { get; set; }
    public virtual ProcurementPolicyExceptionRule? ApprovedExceptionRule { get; set; }
    public virtual ProcurementBudgetCommitment? BudgetCommitment { get; set; }
    public virtual ICollection<ProcurementRequisitionSourcingRelease> SourcingReleases { get; set; } = new List<ProcurementRequisitionSourcingRelease>();
    public virtual ICollection<ProcurementSourcingCase> SourcingCases { get; set; } = new List<ProcurementSourcingCase>();
    public virtual ICollection<PurchaseRequisitionItem> Items { get; set; } = new List<PurchaseRequisitionItem>();

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Items in a purchase requisition
/// </summary>
public class PurchaseRequisitionItem : TenantEntity
{
    [Required]
    public Guid RequisitionId { get; set; }

    public Guid? InventoryItemId { get; set; } // Can be null for non-inventory items

    /// <summary>
    /// Exact approved procurement-plan item that originated this requisition
    /// line. Multi-line requisitions retain one lineage reference per line while
    /// the requisition header keeps the primary item for legacy integrations.
    /// </summary>
    public Guid? SourcePlanItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemDescription { get; set; } = string.Empty;

    [Required]
    public decimal Quantity { get; set; } = 1;

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    [Column(TypeName = "decimal(18,4)")]
    public decimal EstimatedUnitPrice { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; } = 0;

    public DateTime? RequiredDate { get; set; }

    public Guid? PreferredBusinessPartnerId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(1000)]
    public string? Specifications { get; set; }

    // Status tracking
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Ordered, Received, Cancelled

    public Guid? PurchaseOrderId { get; set; } // Link to created PO

    // Navigation Properties
    public virtual PurchaseRequisition Requisition { get; set; } = null!;
    public virtual BusinessPartner? PreferredBusinessPartner { get; set; }
    public virtual PurchaseOrder? PurchaseOrder { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }
    public virtual ProcurementPlanItem? SourcePlanItem { get; set; }
}

#endregion
