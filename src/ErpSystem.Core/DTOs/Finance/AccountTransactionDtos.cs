using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    public sealed class AccountTransactionInquiryPageDto
    {
        public IReadOnlyList<AccountTransactionInquiryItemDto> Items { get; set; } =
            Array.Empty<AccountTransactionInquiryItemDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string AccountingBookCode { get; set; } = string.Empty;
        public string AccountingBookName { get; set; } = string.Empty;
    }

    public sealed class AccountTransactionInquiryItemDto
    {
        public Guid Id { get; set; }
        public Guid JournalEntryId { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public DateTime? PostingDate { get; set; }
        public string? Reference { get; set; }
        public string JournalDescription { get; set; } = string.Empty;
        public string? LineDescription { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string FunctionalCurrencyCode { get; set; } = string.Empty;
        public string? TransactionCurrencyCode { get; set; }
        public decimal? TransactionDebitAmount { get; set; }
        public decimal? TransactionCreditAmount { get; set; }
        public decimal? ForeignAmount { get; set; }
        public Guid? ExchangeRateId { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? ExchangeRateSource { get; set; }
        public DateTime? ExchangeRateDate { get; set; }
        public string AccountingBookCode { get; set; } = string.Empty;
        public string AccountingBookName { get; set; } = string.Empty;
        public Guid? PostingEventId { get; set; }
        public string? SourceModule { get; set; }
        public string? OriginModuleCode { get; set; }
        public Guid? SourceDocumentId { get; set; }
        public string? SourceDocumentType { get; set; }
        public string? SourceReference { get; set; }
        public int LineNumber { get; set; }
        public Guid? FinanceDimensionSetId { get; set; }
        public Guid? FinanceDimensionSnapshotId { get; set; }
        public string? DimensionDisplayValue { get; set; }
        public IReadOnlyList<FinanceDimensionAssignmentDto> Dimensions { get; set; } =
            Array.Empty<FinanceDimensionAssignmentDto>();
    }

    public class AccountTransactionDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? AccountNumber { get; set; }
        public Guid JournalEntryId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = "Debit";
        public string? Description { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Reference { get; set; } = string.Empty;
        public decimal BalanceAfter { get; set; }
        public string? CurrencyCode { get; set; }
        public decimal? ForeignAmount { get; set; }
        public Guid? ExchangeRateId { get; set; }
        public decimal? ExchangeRate { get; set; }
        public int? LineNumber { get; set; }
        public Guid? FinanceDimensionSetId { get; set; }
        public string? FinanceDimensionDisplayValue { get; set; }
        public List<FinanceDimensionAssignmentDto> Dimensions { get; set; } = new();
        public FinanceDimensionSnapshotDto? DimensionSnapshot { get; set; }
    }

    public class CreateAccountTransactionDto
    {
        [Required]
        public Guid AccountId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string TransactionType { get; set; } = "Debit";

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(3)]
        public string? CurrencyCode { get; set; }

        public decimal? ForeignAmount { get; set; }

        public Guid? ExchangeRateId { get; set; }

        public decimal? ExchangeRate { get; set; }

        public int LineNumber { get; set; } = 1;

        /// <summary>
        /// Structured transaction dimensions supplied by the journal maker. Finance resolves the
        /// values into an immutable set; callers cannot select a stored set id.
        /// </summary>
        public IReadOnlyList<FinancePostingDimensionValueDto> Dimensions { get; set; }
            = Array.Empty<FinancePostingDimensionValueDto>();
    }
}
