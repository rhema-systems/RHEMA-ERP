using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.DTOs.Finance
{
    // Account DTOs
    public class AccountDto
    {
        public Guid Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public AccountType AccountType { get; set; }
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public Guid? ParentAccountId { get; set; }
        public string? ParentAccountName { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public int Level { get; set; }
        public string FullPath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateAccountDto
    {
        [Required]
        [MaxLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public AccountType AccountType { get; set; }

        public decimal Balance { get; set; } = 0;

        public Guid? ParentAccountId { get; set; }

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";
    }

    public class UpdateAccountDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; }

        public Guid? ParentAccountId { get; set; }

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "USD";
    }

    // Invoice DTOs
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
        public InvoiceStatus Status { get; set; }
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
        public InvoiceStatus Status { get; set; }
        public string? Notes { get; set; }
        public string? Reference { get; set; }
        public List<UpdateInvoiceLineItemDto> LineItems { get; set; } = new();
    }

    // Invoice Line Item DTOs
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

    // Payment DTOs
    public class PaymentDto
    {
        public Guid Id { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public Guid InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public PaymentStatus Status { get; set; }
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

        public PaymentMethod PaymentMethod { get; set; }

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

    // Journal Entry DTOs
    public class JournalEntryDto
    {
        public Guid Id { get; set; }
        public string JournalNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public JournalStatus Status { get; set; }
        public bool IsReversed { get; set; }
        public Guid? ReversalJournalId { get; set; }
        public DateTime? PostedDate { get; set; }
        public Guid? PostedByUserId { get; set; }
        public string? PostedByUserName { get; set; }
        public List<AccountTransactionDto> Transactions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateJournalEntryDto
    {
        [Required]
        [MaxLength(50)]
        public string JournalNumber { get; set; } = string.Empty;

        [Required]
        public DateTime TransactionDate { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        public List<CreateAccountTransactionDto> Transactions { get; set; } = new();
    }

    // Account Transaction DTOs
    public class AccountTransactionDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? AccountNumber { get; set; }
        public Guid JournalEntryId { get; set; }
        public decimal Amount { get; set; }
        public TransactionType TransactionType { get; set; }
        public string? Description { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Reference { get; set; } = string.Empty;
        public decimal BalanceAfter { get; set; }
    }

    public class CreateAccountTransactionDto
    {
        [Required]
        public Guid AccountId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public TransactionType TransactionType { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string Reference { get; set; } = string.Empty;
    }

    // Search and Filter DTOs
    public class FinanceSearchDto
    {
        public string? SearchTerm { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
    }

    public class InvoiceSearchDto : FinanceSearchDto
    {
        public Guid? CustomerId { get; set; }
        public InvoiceStatus? Status { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public bool? IsOverdue { get; set; }
    }

    public class AccountSearchDto : FinanceSearchDto
    {
        public AccountType? AccountType { get; set; }
        public bool? IsActive { get; set; }
        public Guid? ParentAccountId { get; set; }
        public string? CurrencyCode { get; set; }
    }
}
