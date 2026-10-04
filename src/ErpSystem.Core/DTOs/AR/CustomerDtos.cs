using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.AR;

// ============= Base/Common DTOs =============

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

// ============= Customer DTOs =============

public class CustomerDto
{
    public Guid Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal CustomerCreditBalance { get; set; }
    public int PaymentTermsDays { get; set; }
    public Guid? PaymentTermId { get; set; }
    public string? PriceGroup { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool IsActive { get; set; }
    public bool IsBlacklisted { get; set; }
    public bool IsTransactionReady { get; set; }
    public string ReadinessCode { get; set; } = string.Empty;
    public string ReadinessMessage { get; set; } = string.Empty;
    public DateTime? LastOrderDate { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerCreateDto
{
    public string? CustomerCode { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = "Individual";
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public decimal CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public Guid? PaymentTermId { get; set; }
    public string? PriceGroup { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string? Notes { get; set; }
}

public class CustomerUpdateDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public decimal CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; }
    public Guid? PaymentTermId { get; set; }
    public string? PriceGroup { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class CustomerQueryDto
{
    public string? SearchTerm { get; set; }
    public string? CustomerType { get; set; }
    public bool? IsActive { get; set; }
    public bool IncludeBalances { get; set; }
    public string? TransactionReadiness { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

public class CustomerBalanceDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Days90Plus { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal AvailableCredit { get; set; }
}

public class CreditCheckResultDto
{
    public bool IsApproved { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal CurrentOutstanding { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal AvailableCredit { get; set; }
    public string? Message { get; set; }
}
