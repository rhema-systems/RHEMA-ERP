using System;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance
{
    public class Payment : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string PaymentNumber { get; set; } = string.Empty;

        [Required]
        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        public decimal ExchangeRate { get; set; } = 1.0m;

        // Bank/Payment Details
        [MaxLength(100)]
        public string? BankAccount { get; set; }

        [MaxLength(100)]
        public string? CheckNumber { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }
    }

    public enum PaymentMethod
    {
        Cash = 1,
        Check = 2,
        BankTransfer = 3,
        CreditCard = 4,
        DebitCard = 5,
        PayPal = 6,
        Other = 7
    }

    public enum PaymentStatus
    {
        Pending = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        Refunded = 5
    }
}