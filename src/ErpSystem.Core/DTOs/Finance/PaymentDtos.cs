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

    // ============= AR-Specific Payment DTOs =============

    public class CustomerPaymentDto
    {
        public Guid Id { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal UnallocatedAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public Guid? PaymentMethodId { get; set; }
        public string? PaymentMethodName { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; }
        public Guid? BankAccountId { get; set; }
        public string? BankAccountName { get; set; }
        public Guid? LiquidityAccountId { get; set; }
        public string? LiquidityAccountName { get; set; }
        public Guid? LiquidityAccountEntryId { get; set; }
        public string? CheckNumber { get; set; }
        public string? ChequeDrawerBank { get; set; }
        public string? TransactionReference { get; set; }
        public Guid? WithholdingTaxId { get; set; }
        public Guid? WithholdingTaxAccountId { get; set; }
        public decimal WithholdingTaxAmount { get; set; }
        public Guid? VatWithholdingTaxId { get; set; }
        public Guid? VatWithholdingAccountId { get; set; }
        public decimal VatWithholdingAmount { get; set; }
        public string? WithholdingCertificateNumber { get; set; }
        public DateTime? WithholdingCertificateDate { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ClearedDate { get; set; }
        public bool IsCreditNote { get; set; }
        public Guid? JournalEntryId { get; set; }
        public List<PaymentAllocationDto> Allocations { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentAllocationDto
    {
        public Guid Id { get; set; }
        public Guid CustomerPaymentId { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal AllocatedAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public DateTime AllocationDate { get; set; }
        public string? Notes { get; set; }
        public bool IsReversal { get; set; }
    }
}
