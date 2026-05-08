using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.AR;

// ============= Payment CRUD DTOs =============

public class PaymentCreateDto
{
    public Guid CustomerId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1.0m;
    public Guid? BankAccountId { get; set; }
    public string? CheckNumber { get; set; }
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
    public bool IsCreditNote { get; set; }
}

public class PaymentUpdateDto
{
    public Guid Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? BankAccountId { get; set; }
    public string? CheckNumber { get; set; }
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
}

public class PaymentQueryDto
{
    public string? SearchTerm { get; set; }
    public Guid? CustomerId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? HasUnallocatedAmount { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

// ============= Invoice CRUD DTOs =============

public class InvoiceQueryDto
{
    public string? SearchTerm { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? IsOverdue { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

public class InvoiceCreateDto
{
    public Guid CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal DiscountAmount { get; set; }
    public List<InvoiceLineItemCreateDto> LineItems { get; set; } = new();
}

public class InvoiceLineItemCreateDto
{
    public string LineItemType { get; set; } = "Product";
    public Guid? ProductId { get; set; }
    public Guid? GLAccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? TaxCode { get; set; }
    public string? Unit { get; set; }
    public decimal DiscountPercentage { get; set; }
}

public class InvoiceUpdateDto
{
    public Guid Id { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public decimal DiscountAmount { get; set; }
    public List<InvoiceLineItemUpdateDto> LineItems { get; set; } = new();
}

public class InvoiceLineItemUpdateDto
{
    public Guid? Id { get; set; }
    public string LineItemType { get; set; } = "Product";
    public Guid? ProductId { get; set; }
    public Guid? GLAccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? TaxCode { get; set; }
    public string? Unit { get; set; }
    public decimal DiscountPercentage { get; set; }
}

// ============= Payment Allocation DTOs =============

public class PaymentAllocation_CreateDto
{
    public Guid CustomerPaymentId { get; set; }
    public List<InvoiceAllocationDto> Allocations { get; set; } = new();
}

public class InvoiceAllocationDto
{
    public Guid InvoiceId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
}

public class PaymentAllocationResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public decimal TotalAllocated { get; set; }
    public decimal RemainingUnallocated { get; set; }
    public List<ErpSystem.Core.DTOs.Finance.PaymentAllocationDto> Allocations { get; set; } = new();
}

public class CreditNoteCreateDto
{
    public Guid CustomerId { get; set; }
    public DateTime CreditNoteDate { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class OutstandingInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public int DaysOverdue { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}
