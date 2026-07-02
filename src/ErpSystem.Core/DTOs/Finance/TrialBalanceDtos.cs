using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Trial Balance output DTO - lists all accounts with debit/credit balances
    /// </summary>
    public class TrialBalanceDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public DateTime AsAtDate { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public string CurrencyCode { get; set; } = "GHS";
        
        public List<TrialBalanceLineDto> Lines { get; set; } = new();
        
        // Summary totals
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        
        // Validation - books are balanced when debits = credits
        public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
        public decimal Difference => TotalDebits - TotalCredits;
    }

    /// <summary>
    /// Individual account line in Trial Balance
    /// </summary>
    public class TrialBalanceLineDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        
        // Helper to determine which column to show balance in
        public decimal NetBalance => DebitBalance - CreditBalance;
    }

    /// <summary>
    /// Request DTO for generating Trial Balance
    /// </summary>
    public class TrialBalanceRequestDto
    {
        public DateTime AsAtDate { get; set; } = DateTime.UtcNow.Date;
        public string BookClassification { get; set; } = "IFRS";
        public bool IncludeZeroBalances { get; set; } = false;
    }
}
