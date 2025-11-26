using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    public class PaymentDto
    {
        public Guid Id { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public Guid InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "Completed";
        public string CurrencyCode { get; set; } = "USD";
        public decimal ExchangeRate { get; set; } = 1.0m;
        public string? BankAccount { get; set; }
        public string? CheckNumber { get; set; }
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreatePaymentDto
    {
        [Required]
        [MaxLength(50)]
        public string PaymentNumber { get; set; } = string.Empty;

        [Required]
        public Guid InvoiceId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        public string PaymentMethod { get; set; } = "Cash";

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        public decimal ExchangeRate { get; set; } = 1.0m;

        [MaxLength(100)]
        public string? BankAccount { get; set; }

        [MaxLength(100)]
        public string? CheckNumber { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }
    }
}
