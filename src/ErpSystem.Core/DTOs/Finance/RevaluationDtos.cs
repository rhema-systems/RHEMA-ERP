using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance
{
    public class RevaluationRequestDto
    {
        [Required]
        public DateTime RevaluationDate { get; set; }

        [Required]
        public string RevaluationType { get; set; } = "Month-End"; // Month-End, Year-End, Ad-hoc

        public string? CurrencyCode { get; set; } // Optional: specific currency or all

        [Required]
        public Guid UnrealizedGainLossAccountId { get; set; }

        public bool PreviewOnly { get; set; } = false;

        /// <summary>
        /// Optional fingerprint returned by preview. When supplied for posting, the
        /// service rejects any change in exposure balances or closing-rate evidence.
        /// </summary>
        [MaxLength(64)]
        public string? ExpectedPreviewFingerprint { get; set; }
    }
    public class CurrencyRevaluationResultDto
    {
        public Guid? JournalEntryId { get; set; }
        public decimal TotalGainLoss { get; set; }
        public int ProcessedAccountsCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }

    public sealed class CurrencyRevaluationPreviewDto
    {
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime RevaluationDate { get; set; }
        public string FunctionalCurrencyCode { get; set; } = string.Empty;
        public decimal TotalGainAmount { get; set; }
        public decimal TotalLossAmount { get; set; }
        public decimal NetGainLossAmount { get; set; }
        public int ExposureCount { get; set; }
        public string PreviewFingerprint { get; set; } = string.Empty;
        public List<CurrencyRevaluationPreviewLineDto> Lines { get; set; } = new();
    }

    /// <summary>
    /// Stable response returned after a revaluation journal has posted. This deliberately
    /// excludes EF navigation properties so a successful posting cannot be reported as a
    /// client-side JSON failure while serializing the journal entity graph.
    /// </summary>
    public sealed class CurrencyRevaluationPostingResultDto
    {
        public Guid Id { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public string PostingStatus { get; set; } = string.Empty;
        public decimal TotalDebitAmount { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public DateTime? PostingDate { get; set; }
    }

    public sealed class CurrencyRevaluationPreviewLineDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string SourceModule { get; set; } = string.Empty;
        public string TransactionCurrency { get; set; } = string.Empty;
        public string FunctionalCurrencyCode { get; set; } = string.Empty;
        public decimal ForeignCurrencyBalance { get; set; }
        public decimal CarryingFunctionalAmount { get; set; }
        public decimal PreviousRate { get; set; }
        public decimal ClosingExchangeRate { get; set; }
        public decimal RevaluedFunctionalAmount { get; set; }
        public decimal GainLossAmount { get; set; }
        public string GainLossType { get; set; } = string.Empty;
        public string RevaluationFrequency { get; set; } = string.Empty;
        public string RateType { get; set; } = string.Empty;
        public string QuoteSide { get; set; } = string.Empty;
    }

    public sealed class FxRevaluationBatchSummaryDto
    {
        public Guid Id { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime RevaluationDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string FunctionalCurrencyCode { get; set; } = string.Empty;
        public List<string> Currencies { get; set; } = new();
        public int ExposureCount { get; set; }
        public decimal TotalGainAmount { get; set; }
        public decimal TotalLossAmount { get; set; }
        public decimal NetGainLossAmount { get; set; }
        public Guid? JournalEntryId { get; set; }
        public string? JournalEntryNumber { get; set; }
        public Guid? ReversalJournalEntryId { get; set; }
        public string? ReversalJournalEntryNumber { get; set; }
        public DateTime? PostedAt { get; set; }
        public DateTime? ReversedAt { get; set; }
    }

    public sealed class ReverseFxRevaluationRequestDto
    {
        [Required]
        public DateTime ReversalDate { get; set; }

        [Required]
        [MinLength(5)]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
