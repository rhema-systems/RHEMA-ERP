using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance
{
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid BusinessPartnerId { get; set; }
        public Guid BusinessPartnerRoleId { get; set; }
        public Guid BusinessPartnerArProfileVersionId { get; set; }
        public string BusinessPartnerCode { get; set; } = string.Empty;
        public string? BusinessPartnerLegalName { get; set; }
        public string? BusinessPartnerTin { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerAddress { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountReason { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal RoundingAdjustmentAmount { get; set; }
        public Guid? FinanceRoundingEvidenceId { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public string Status { get; set; } = "Draft";
        public bool ApprovalRequired { get; set; } = true;
        public Guid? WorkflowInstanceId { get; set; }
        public string? Notes { get; set; }
        public string? Reference { get; set; }
        public bool IsOpeningBalance { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public string? CurrencyOverrideReason { get; set; }
        public decimal ExchangeRate { get; set; } = 1.0m;
        public Guid? ExchangeRateId { get; set; }
        public int PaymentTermsDays { get; set; } = 30; // AR-specific
        public Guid? PaymentTermId { get; set; }
        public decimal EarlyPaymentDiscountPercentage { get; set; }
        public DateTime? EarlyPaymentDiscountDueDate { get; set; }
        public decimal EarlyPaymentDiscountAmount { get; set; }
        public Guid? TaxGroupId { get; set; }
        public Guid? JournalEntryId { get; set; }
        public List<InvoiceLineItemDto> LineItems { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
        public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateInvoiceDto
    {
        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public Guid BusinessPartnerId { get; set; }
        public Guid? BusinessPartnerRoleId { get; set; }

        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? CustomerAddress { get; set; }

        [Required]
        public DateTime InvoiceDate { get; set; }

        public DateTime? DueDate { get; set; }
        public Guid? PaymentTermId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        public bool IsOpeningBalance { get; set; }

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";

        [MaxLength(500)]
        public string? CurrencyOverrideReason { get; set; }

        public decimal ExchangeRate { get; set; } = 1.0m;

        public Guid? TaxGroupId { get; set; }

        public List<CreateInvoiceLineItemDto> LineItems { get; set; } = new();
    }

    public class UpdateInvoiceDto
    {
        public Guid BusinessPartnerId { get; set; }
        public Guid? BusinessPartnerRoleId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerAddress { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? PaymentTermId { get; set; }
        public string Status { get; set; } = "Draft";
        public string? Notes { get; set; }
        public string? Reference { get; set; }
        public bool IsOpeningBalance { get; set; }
        [MaxLength(500)]
        public string? CurrencyOverrideReason { get; set; }
        public Guid? TaxGroupId { get; set; }
        public List<UpdateInvoiceLineItemDto> LineItems { get; set; } = new();
    }

    public class InvoiceLineItemDto
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public string LineItemType { get; set; } = "Product";
        public Guid? ProductId { get; set; }
        public Guid? GLAccountId { get; set; }
        public string? GLAccountCode { get; set; }
        public string? GLAccountName { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public string? TaxCode { get; set; } // AR-specific
        public Guid? TaxGroupId { get; set; }
        public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;
        public string? Unit { get; set; }
        public decimal DiscountPercentage { get; set; } // AR-specific
        public decimal DiscountAmount { get; set; } // AR-specific
    }

    public class CreateInvoiceLineItemDto
    {
        [Required]
        public string LineItemType { get; set; } = "Product";

        public Guid? ProductId { get; set; }

        public Guid? GLAccountId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; } = 1;

        [Required]
        public decimal UnitPrice { get; set; }

        public decimal TaxRate { get; set; } = 0;

        public string? TaxCode { get; set; } // AR-specific
        public Guid? TaxGroupId { get; set; }
        public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;

        [Range(typeof(decimal), "0", "100")]
        public decimal DiscountPercentage { get; set; } = 0; // AR line trade discount

        [MaxLength(50)]
        public string? Unit { get; set; }
    }

    public class UpdateInvoiceLineItemDto : CreateInvoiceLineItemDto
    {
        public Guid Id { get; set; }
    }
}
