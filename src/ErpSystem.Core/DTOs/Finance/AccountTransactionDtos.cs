using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
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
        public decimal? ExchangeRate { get; set; }
        public int? LineNumber { get; set; }
        public Guid? FinanceDimensionSetId { get; set; }
        public string? FinanceDimensionDisplayValue { get; set; }
        public List<FinanceDimensionAssignmentDto> Dimensions { get; set; } = new();
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
