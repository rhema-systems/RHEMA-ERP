using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Finance;

public enum SupplierReturnStatus
{
    Draft = 1,
    Approved = 2,
    Cancelled = 3,
    PendingApproval = 4,
    Rejected = 5
}

public enum SupplierDebitNoteStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

public class SupplierReturn : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    [Required]
    public Guid VendorId { get; set; }
    public virtual BusinessPartner Vendor { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string VendorName { get; set; } = string.Empty;

    public Guid? OriginalVendorInvoiceId { get; set; }
    public virtual VendorInvoice? OriginalVendorInvoice { get; set; }

    public Guid? OriginalFinancePurchaseOrderReceiptId { get; set; }
    public virtual FinancePurchaseOrderReceipt? OriginalFinancePurchaseOrderReceipt { get; set; }

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Reason { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCurrencyAmount { get; set; }

    public SupplierReturnStatus Status { get; set; } = SupplierReturnStatus.Draft;

    public virtual ICollection<SupplierReturnLineItem> LineItems { get; set; } = new List<SupplierReturnLineItem>();
}

public class SupplierReturnLineItem : TenantEntity
{
    [Required]
    public Guid SupplierReturnId { get; set; }
    public virtual SupplierReturn SupplierReturn { get; set; } = null!;

    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityReturned { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

public class SupplierDebitNote : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string DebitNoteNumber { get; set; } = string.Empty;

    [Required]
    public Guid VendorId { get; set; }
    public virtual BusinessPartner Vendor { get; set; } = null!;

    public Guid? SupplierReturnId { get; set; }
    public virtual SupplierReturn? SupplierReturn { get; set; }

    public Guid? OriginalVendorInvoiceId { get; set; }
    public virtual VendorInvoice? OriginalVendorInvoice { get; set; }

    public DateTime DebitNoteDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCurrencyAmount { get; set; }

    public SupplierDebitNoteStatus Status { get; set; } = SupplierDebitNoteStatus.Draft;

    public Guid? JournalEntryId { get; set; }

    public virtual ICollection<SupplierDebitNoteLineItem> LineItems { get; set; } = new List<SupplierDebitNoteLineItem>();
}

public class SupplierDebitNoteLineItem : TenantEntity
{
    [Required]
    public Guid SupplierDebitNoteId { get; set; }
    public virtual SupplierDebitNote SupplierDebitNote { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}
