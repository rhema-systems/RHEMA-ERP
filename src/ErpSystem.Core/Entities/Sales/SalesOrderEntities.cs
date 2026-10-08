using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Sales;

#region Sales Order

/// <summary>
/// Master sales order — the core transactional entity of the order-to-cash cycle.
/// Links to BusinessPartner (customer), Opportunity/Quote for traceability,
/// and drives Delivery Notes and Invoices downstream.
/// </summary>
public class SalesOrder : DocumentEntity
{
    // ── Customer (via unified BusinessPartner) ──────────────────────────

    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    // ── Traceability ────────────────────────────────────────────────────

    /// <summary>
    /// Quote that was converted into this Sales Order (if any)
    /// </summary>
    public Guid? QuoteId { get; set; }
    public virtual Quote? Quote { get; set; }

    /// <summary>
    /// Opportunity this order is associated with (if any)
    /// </summary>
    public Guid? OpportunityId { get; set; }
    public virtual Opportunity? Opportunity { get; set; }

    // ── Order Classification ────────────────────────────────────────────

    public SalesOrderType OrderType { get; set; } = SalesOrderType.Standard;

    public SalesOrderStatus OrderStatus { get; set; } = SalesOrderStatus.Draft;

    [MaxLength(50)]
    public string? OrderPriority { get; set; } = "Normal"; // Low, Normal, High, Urgent

    // ── Sales Rep Assignment ────────────────────────────────────────────

    public Guid? SalesRepId { get; set; }
    public virtual ApplicationUser? SalesRep { get; set; }

    // ── Financial ───────────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingAmount { get; set; }

    // TotalAmount, TaxAmount, Currency, ExchangeRate inherited from FinancialEntity/DocumentEntity

    // ── Payment Terms ───────────────────────────────────────────────────

    public Guid? PaymentTermId { get; set; }
    public virtual PaymentTerm? PaymentTerm { get; set; }

    public int PaymentTermsDays { get; set; } = 30;

    // ── Delivery Details ────────────────────────────────────────────────

    public DateTime? RequestedDeliveryDate { get; set; }

    public DateTime? PromisedDeliveryDate { get; set; }

    public DateTime? ActualDeliveryDate { get; set; }

    public ShipmentMethod? ShipmentMethod { get; set; }

    [MaxLength(500)]
    public string? ShippingAddress { get; set; }

    [MaxLength(500)]
    public string? BillingAddress { get; set; }

    [MaxLength(500)]
    public string? DeliveryInstructions { get; set; }

    // ── Warehouse (default source for fulfillment) ──────────────────────

    public Guid? WarehouseId { get; set; }
    public virtual Warehouse? Warehouse { get; set; }

    // ── Property Reference (TDC-specific) ───────────────────────────────

    /// <summary>
    /// For property sales: reference to the property/plot being sold
    /// </summary>
    [MaxLength(100)]
    public string? PropertyReference { get; set; }

    public PropertyType? PropertyType { get; set; }

    // ── Invoice Link ────────────────────────────────────────────────────

    public Guid? InvoiceId { get; set; }
    public virtual Invoice? Invoice { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    [MaxLength(100)] public string? InvoiceGenerationKey { get; set; }
    [MaxLength(64)] public string? InvoiceGenerationHash { get; set; }
    public Guid? InvoiceGeneratedById { get; set; }
    public string? InvoiceEconomicsJson { get; set; }
    public string? InvoiceSourceJson { get; set; }

    public Guid? TaxGroupId { get; set; }
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    // ── Approval (inherited from ApprovableEntity via DocumentEntity) ───
    // ApprovalStatus, SubmittedById, SubmittedDate, ApprovedById, ApprovedDate, ApprovalComments

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation Properties ───────────────────────────────────────────

    public virtual ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    public virtual ICollection<SalesOrderStatusHistory> StatusHistory { get; set; } = new List<SalesOrderStatusHistory>();
    public virtual ICollection<DeliveryNote> DeliveryNotes { get; set; } = new List<DeliveryNote>();
    public virtual ICollection<SalesOrderCustomerDeposit> CustomerDeposits { get; set; } = new List<SalesOrderCustomerDeposit>();
}

#endregion

/// <summary>
/// Durable lineage between a property Sales Order and the canonical posted AR customer advance.
/// Tender evidence remains structured so Finance reports do not depend on browser-composed text.
/// </summary>
public sealed class SalesOrderCustomerDeposit : TenantEntity
{
    public Guid SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public Guid CustomerPaymentId { get; set; }
    public CustomerPayment CustomerPayment { get; set; } = null!;

    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string TenderType { get; set; } = string.Empty;

    [MaxLength(150)] public string? ExternalBankName { get; set; }
    [MaxLength(100)] public string? ExternalAccountNumber { get; set; }
    [MaxLength(100)] public string? ChequeNumber { get; set; }
    [MaxLength(100)] public string? DepositReference { get; set; }
    [MaxLength(100)] public string? IdentificationReference { get; set; }

    [Required, MaxLength(500)]
    public string PropertyDescription { get; set; } = string.Empty;
}

#region Sales Order Line

/// <summary>
/// Individual line item on a Sales Order.
/// Can reference a Product (from Sales catalog) or an InventoryItem for stock fulfillment.
/// </summary>
public class SalesOrderLine : BaseEntity, ErpSystem.Core.Interfaces.Inventory.ICommercialQuantityEvidenceLine
{
    [Required]
    public Guid SalesOrderId { get; set; }
    public virtual SalesOrder SalesOrder { get; set; } = null!;

    public int LineNumber { get; set; } = 1;

    // ── Product / Inventory Reference ───────────────────────────────────

    public Guid? ProductId { get; set; }
    public virtual Product? Product { get; set; }

    public Guid? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    // ── Description ─────────────────────────────────────────────────────

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ProductCode { get; set; }

    // ── Quantity & Pricing ──────────────────────────────────────────────

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,4)")]
    public decimal DeliveredQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal InvoicedQuantity { get; set; } = 0;

    [NotMapped]
    public decimal RemainingQuantity => Quantity - DeliveredQuantity;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;

    // ── Discount ────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    // ── Tax ──────────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [MaxLength(50)]
    public string? TaxCode { get; set; }

    public Guid? TaxGroupId { get; set; }
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    // ── UOM ──────────────────────────────────────────────────────────────

    [MaxLength(50)]
    public string? Unit { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    [MaxLength(20)] public string? UnitOfMeasureCodeSnapshot { get; set; }
    public int? UnitOfMeasureDecimalPlacesSnapshot { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? UnitOfMeasureRoundingIncrementSnapshot { get; set; }

    // ── Stock Reservation ───────────────────────────────────────────────

    public Guid? WarehouseId { get; set; }
    public virtual Warehouse? Warehouse { get; set; }

    public Guid? LocationId { get; set; }
    public virtual WarehouseLocation? Location { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Whether stock has been reserved for this line
    /// </summary>
    public bool IsStockReserved { get; set; } = false;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReservedQuantity { get; set; } = 0;

    // ── Cost Tracking ───────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,4)")]
    public decimal? UnitCost { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? CostTotal { get; set; }

    // ── Notes ────────────────────────────────────────────────────────────

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── GL Account (for service/non-inventory lines) ────────────────────

    public Guid? GLAccountId { get; set; }
    public virtual Account? GLAccount { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Sales Order Status History

/// <summary>
/// Audit trail for Sales Order status transitions
/// </summary>
public class SalesOrderStatusHistory : BaseEntity
{
    [Required]
    public Guid SalesOrderId { get; set; }
    public virtual SalesOrder SalesOrder { get; set; } = null!;

    public SalesOrderStatus? FromStatus { get; set; }

    [Required]
    public SalesOrderStatus ToStatus { get; set; }

    public Guid? ChangedById { get; set; }
    public virtual ApplicationUser? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion
