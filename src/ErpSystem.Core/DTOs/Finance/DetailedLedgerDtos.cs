using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance
{
    public class DetailedLedgerCurrencyTotalDto
    {
        public string CurrencyCode { get; set; } = "GHS";
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal ClosingBalance { get; set; }
    }

    public class DetailedLedgerRequestDto
    {
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
        public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;
        public List<Guid> AccountIds { get; set; } = new();
        public string BookClassification { get; set; } = "IFRS";
        public bool IncludeReversed { get; set; } = true;
        public bool IncludeOpeningBalances { get; set; } = true;
        public List<FinanceSegmentFilterDto> SegmentFilters { get; set; } = new();
        public List<FinanceDimensionFilterDto> DimensionFilters { get; set; } = new();
    }

    public class DetailedLedgerReportDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; } = DateTime.UtcNow;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public string CurrencyCode { get; set; } = "GHS";
        public List<DetailedLedgerAccountDto> Accounts { get; set; } = new();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
    }

    public class DetailedLedgerAccountDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public string OpeningBalanceType { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal ClosingBalance { get; set; }
        public string ClosingBalanceType { get; set; } = string.Empty;
        public List<DetailedLedgerLineDto> Lines { get; set; } = new();
    }

    public class DetailedLedgerLineDto
    {
        public Guid TransactionId { get; set; }
        public Guid JournalEntryId { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public int LineNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string SourceModule { get; set; } = string.Empty;
        public string PostingStatus { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal RunningBalance { get; set; }
        public string RunningBalanceType { get; set; } = string.Empty;
        public string? CurrencyCode { get; set; }
        public decimal? ForeignAmount { get; set; }
        public decimal? ExchangeRate { get; set; }
        public bool IsReversed { get; set; }
        public string? SegmentString { get; set; }
        public Guid? FinanceDimensionSetId { get; set; }
        public string? FinanceDimensionDisplay { get; set; }
        public List<FinanceDimensionAssignmentDto> Dimensions { get; set; } = new();
    }
}
