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
        public List<CurrencyRevaluationPreviewLineDto> Lines { get; set; } = new();
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
    }
}
