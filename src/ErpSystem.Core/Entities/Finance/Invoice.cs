using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance
{
    public class Invoice : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public Guid BusinessPartnerId { get; set; }
        public virtual BusinessPartner? BusinessPartner { get; set; }

        [NotMapped]
        public Guid CustomerId
        {
            get => BusinessPartnerId;
            set => BusinessPartnerId = value;
        }

        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? CustomerAddress { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; }

        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditedAmount { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAmount => TotalAmount - PaidAmount - CreditedAmount;

        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        public bool IsOpeningBalance { get; set; }

        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1.0m;

        /// <summary>
        /// Total amount converted to the Tenant's Base Currency.
        /// Used for reporting and credit limit checks.
        /// Formula: TotalAmount * ExchangeRate (if using direct quote)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseCurrencyAmount { get; set; }

        // Payment terms
        public int PaymentTermsDays { get; set; } = 30;
        public Guid? PaymentTermId { get; set; }
        public virtual PaymentTerm? PaymentTerm { get; set; }

        // Taxation
        public Guid? TaxGroupId { get; set; }
        
        [ForeignKey(nameof(TaxGroupId))]
        public virtual TaxGroup? TaxGroup { get; set; }

        public Guid? JournalEntryId { get; set; }

        // Multi-tenant
        public Guid TenantId { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;

        // Navigation properties
        public virtual ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
        public virtual ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
        
        // Keep legacy payments collection for backward compatibility
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class InvoiceLineItem : BaseEntity
    {
        [Required]
        public Guid InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; } = null!;

        // Line item type: Product or GL Account
        [Required]
        public LineItemType LineItemType { get; set; } = LineItemType.Product;

        // For product-based line items
        public Guid? ProductId { get; set; }

        public Guid? InventoryItemId { get; set; }
        public virtual InventoryItem? InventoryItem { get; set; }

        // Inventory source/destination tracking
        public Guid? WarehouseId { get; set; }
        public virtual Warehouse? Warehouse { get; set; }
        public Guid? LocationId { get; set; }
        public virtual WarehouseLocation? Location { get; set; }

        // Inventory item tracking details (required based on InventoryItem settings)
        [MaxLength(100)]
        public string? SerialNumber { get; set; }
        [MaxLength(100)]
        public string? LotNumber { get; set; }
        public DateTime? ExpirationDate { get; set; }

        // Cost tracking (populated at invoice send for COGS)
        [Column(TypeName = "decimal(18,4)")]
        public decimal? UnitCost { get; set; }  // WAC or FIFO cost at time of issue
        [Column(TypeName = "decimal(18,4)")]
        public decimal? CostTotal { get; set; } // Quantity × UnitCost

        // For GL account-based line items
        public Guid? GLAccountId { get; set; }
        public virtual Account? GLAccount { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal => Quantity * UnitPrice;

        public Guid? TaxGroupId { get; set; }
        
        [ForeignKey(nameof(TaxGroupId))]
        public virtual TaxGroup? TaxGroup { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [MaxLength(50)]
        public string? TaxCode { get; set; }

        [MaxLength(50)]
        public string? Unit { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercentage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        // Multi-tenant
        public Guid TenantId { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;
    }

    public enum InvoiceStatus
    {
        Draft = 1,
        Sent = 2,
        PartiallyPaid = 3,
        Paid = 4,
        Overdue = 5,
        Cancelled = 6
    }
}
