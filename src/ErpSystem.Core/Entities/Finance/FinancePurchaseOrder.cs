using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Finance;

public enum FinancePurchaseOrderReceiptStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5
}

/// <summary>
/// Finance/AP purchase order used for payable commitment and matching flows.
/// Supports both inventory-backed and GL-account-backed lines.
/// </summary>
public class FinancePurchaseOrder : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public Guid VendorId { get; set; }
    public virtual BusinessPartner Vendor { get; set; } = null!;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDeliveryDate { get; set; }
    public Guid? PaymentTermId { get; set; }
    public virtual PaymentTerm? PaymentTerm { get; set; }

    /// <summary>
    /// 1 Draft, 2 Approved, 3 PartiallyReceived, 4 Received, 5 PartiallyInvoiced,
    /// 6 Invoiced, 7 Closed, 8 Cancelled, 9 PendingApproval, 10 Rejected.
    /// </summary>
    public int Status { get; set; } = 1;

    public bool ApprovalRequired { get; set; } = true;

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    public Guid? TaxGroupId { get; set; }
    public virtual TaxGroup? TaxGroup { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public virtual ICollection<FinancePurchaseOrderItem> Items { get; set; } = new List<FinancePurchaseOrderItem>();
    public virtual ICollection<FinancePurchaseOrderReceipt> Receipts { get; set; } = new List<FinancePurchaseOrderReceipt>();
}

public class FinancePurchaseOrderItem : TenantEntity
{
    public Guid FinancePurchaseOrderId { get; set; }
    public virtual FinancePurchaseOrder FinancePurchaseOrder { get; set; } = null!;

    /// <summary>
    /// 1 Inventory, 2 GL account.
    /// </summary>
    public int LineType { get; set; } = 1;

    public Guid? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    public Guid? WarehouseId { get; set; }

    public Guid? GlAccountId { get; set; }
    public virtual Account? GlAccount { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal OrderedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal InvoicedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CancelledQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [MaxLength(50)]
    public string? TaxCode { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    public Guid? TaxGroupId { get; set; }
    public virtual TaxGroup? TaxGroup { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

public class FinancePurchaseOrderReceipt : TenantEntity
{
    public Guid FinancePurchaseOrderId { get; set; }
    public virtual FinancePurchaseOrder FinancePurchaseOrder { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    public FinancePurchaseOrderReceiptStatus Status { get; set; } = FinancePurchaseOrderReceiptStatus.Draft;

    public bool ApprovalRequired { get; set; } = true;

    public Guid? WorkflowInstanceId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? SubmittedById { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedById { get; set; }

    public DateTime? RejectedAt { get; set; }

    public Guid? RejectedById { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    public Guid? VendorInvoiceId { get; set; }
    public virtual VendorInvoice? VendorInvoice { get; set; }

    public virtual ICollection<FinancePurchaseOrderReceiptItem> Items { get; set; } = new List<FinancePurchaseOrderReceiptItem>();
}

public class FinancePurchaseOrderReceiptItem : TenantEntity
{
    public Guid FinancePurchaseOrderReceiptId { get; set; }
    public virtual FinancePurchaseOrderReceipt FinancePurchaseOrderReceipt { get; set; } = null!;

    public Guid FinancePurchaseOrderItemId { get; set; }
    public virtual FinancePurchaseOrderItem FinancePurchaseOrderItem { get; set; } = null!;

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityReceived { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal InvoicedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }
}
