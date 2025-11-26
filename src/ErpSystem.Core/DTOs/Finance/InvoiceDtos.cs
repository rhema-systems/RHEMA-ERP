using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerAddress { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public string Status { get; set; } = "Draft";
        public string? Notes { get; set; }
        public string? Reference { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public decimal ExchangeRate { get; set; } = 1.0m;
        public List<InvoiceLineItemDto> LineItems { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateInvoiceDto
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

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        public decimal ExchangeRate { get; set; } = 1.0m;

        public List<CreateInvoiceLineItemDto> LineItems { get; set; } = new();
    }

    public class UpdateInvoiceDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerAddress { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; } = "Draft";
        public string? Notes { get; set; }
        public string? Reference { get; set; }
        public List<UpdateInvoiceLineItemDto> LineItems { get; set; } = new();
    }

    public class InvoiceLineItemDto
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public Guid? ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public string? Unit { get; set; }
    }

    public class CreateInvoiceLineItemDto
    {
        public Guid? ProductId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; } = 1;

        [Required]
        public decimal UnitPrice { get; set; }

        public decimal TaxRate { get; set; } = 0;

        [MaxLength(50)]
        public string? Unit { get; set; }
    }

    public class UpdateInvoiceLineItemDto : CreateInvoiceLineItemDto
    {
        public Guid Id { get; set; }
    }
}
