using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance
{
    public class MultiCurrencyDetailRequestDto
    {
        public Guid? AccountId { get; set; }
        public string? CurrencyCode { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IncludeRevaluation { get; set; } = true;
    }

    public class MultiCurrencyDetailReportDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<MultiCurrencyAccountDetailDto> Accounts { get; set; } = new();
    }

    public class MultiCurrencyAccountDetailDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        
        public decimal OpeningBalanceForeign { get; set; }
        public decimal OpeningBalanceBase { get; set; }
        
        public decimal TotalDebitsForeign { get; set; }
        public decimal TotalCreditsForeign { get; set; }
        public decimal TotalDebitsBase { get; set; }
        public decimal TotalCreditsBase { get; set; }
        
        public decimal ClosingBalanceForeign { get; set; }
        public decimal ClosingBalanceBase { get; set; }
        
        public decimal UnrealizedGainLoss { get; set; }

        public List<MultiCurrencyTransactionDetailDto> Transactions { get; set; } = new();
    }

    public class MultiCurrencyTransactionDetailDto
    {
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty; // Debit/Credit
        
        public decimal ForeignAmount { get; set; }
        public decimal ExchangeRate { get; set; }
        public decimal BaseAmount { get; set; }
        
        public decimal RunningBalanceForeign { get; set; }
        public decimal RunningBalanceBase { get; set; }
        
        public bool IsRevaluation { get; set; }
    }
}
