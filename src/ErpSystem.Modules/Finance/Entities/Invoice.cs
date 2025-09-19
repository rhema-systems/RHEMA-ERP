using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Modules.Finance.Entities
{
    public class Invoice : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public Guid CustomerId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? CustomerAddress { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; }

        public DateTime? DueDate { get; set; }

        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount => TotalAmount - PaidAmount;

        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        public decimal ExchangeRate { get; set; } = 1.0m;

        public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class InvoiceLineItem : BaseEntity
    {
        [Required]
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public Guid? ProductId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; } = 1;

        [Required]
        public decimal UnitPrice { get; set; }

        public decimal LineTotal => Quantity * UnitPrice;

        public decimal TaxRate { get; set; }
        public decimal TaxAmount => LineTotal * (TaxRate / 100);

        [MaxLength(50)]
        public string? Unit { get; set; }
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