using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Finance;

public enum FinancePurchaseOrderStatus
{
    Draft = 1,
    Approved = 2,
    PartiallyReceived = 3,
    Received = 4,
    PartiallyInvoiced = 5,
    Invoiced = 6,
    Closed = 7,
    Cancelled = 8
}

public enum FinancePurchaseOrderLineType
{
    Inventory = 1,
    GLAccount = 2
}

public class FinancePurchaseOrder : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public Guid VendorId { get; set; }
    public BusinessPartner? Vendor { get; set; }

    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    public FinancePurchaseOrderStatus Status { get; set; } = FinancePurchaseOrderStatus.Draft;

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";
    
    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    
    [MaxLength(500)]
    public string? Remarks { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    // Taxation
    public Guid? TaxGroupId { get; set; }
    
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    public ICollection<FinancePurchaseOrderItem> Items { get; set; } = new List<FinancePurchaseOrderItem>();
    public ICollection<FinancePurchaseOrderReceipt> Receipts { get; set; } = new List<FinancePurchaseOrderReceipt>();
}

public class FinancePurchaseOrderItem : TenantEntity
{
    public Guid FinancePurchaseOrderId { get; set; }
    public FinancePurchaseOrder? FinancePurchaseOrder { get; set; }

    public FinancePurchaseOrderLineType LineType { get; set; }

    public Guid? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    // Required for Inventory LineType
    public Guid? WarehouseId { get; set; }
    // public Warehouse? Warehouse { get; set; }

    public Guid? GlAccountId { get; set; }
    public Account? GlAccount { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal OrderedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal InvoicedQuantity { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CancelledQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }
    
    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [MaxLength(50)]
    public string? TaxCode { get; set; }

    public Guid? TaxGroupId { get; set; }
    
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxRate { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }
}

public class FinancePurchaseOrderReceipt : TenantEntity
{
    public Guid FinancePurchaseOrderId { get; set; }
    public FinancePurchaseOrder? FinancePurchaseOrder { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }
    
    [MaxLength(500)]
    public string? Remarks { get; set; }

    public Guid? VendorInvoiceId { get; set; }
    public VendorInvoice? VendorInvoice { get; set; }

    public ICollection<FinancePurchaseOrderReceiptItem> Items { get; set; } = new List<FinancePurchaseOrderReceiptItem>();
}

public class FinancePurchaseOrderReceiptItem : TenantEntity
{
    public Guid FinancePurchaseOrderReceiptId { get; set; }
    public FinancePurchaseOrderReceipt? FinancePurchaseOrderReceipt { get; set; }

    public Guid FinancePurchaseOrderItemId { get; set; }
    public FinancePurchaseOrderItem? FinancePurchaseOrderItem { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantityReceived { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal InvoicedQuantity { get; set; }
    
    [NotMapped]
    public decimal RemainingToInvoice => QuantityReceived - InvoicedQuantity;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }
}
