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
}
