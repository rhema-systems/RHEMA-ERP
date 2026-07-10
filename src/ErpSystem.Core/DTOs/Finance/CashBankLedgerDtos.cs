using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance;

public class CashBankLedgerRequestDto
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;
    public List<Guid> BankAccountIds { get; set; } = new();
    public List<Guid> GlAccountIds { get; set; } = new();
    public string BookClassification { get; set; } = "IFRS";
    public bool IncludeOpeningBalances { get; set; } = true;
    public List<FinanceSegmentFilterDto> SegmentFilters { get; set; } = new();
}

public class CashBankLedgerReportDto
{
    public string CompanyName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BookClassification { get; set; } = "IFRS";
    public string CurrencyCode { get; set; } = "GHS";
    public List<CashBankLedgerAccountDto> Accounts { get; set; } = new();
    public decimal TotalOpeningBalance { get; set; }
    public decimal TotalReceipts { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal TotalClosingBalance { get; set; }
}

public class CashBankLedgerAccountDto
{
    public Guid BankAccountId { get; set; }
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public Guid GlAccountId { get; set; }
    public string GlAccountNumber { get; set; } = string.Empty;
    public string GlAccountName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal Receipts { get; set; }
    public decimal Payments { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal StoredSnapshotBalance { get; set; }
    public decimal SnapshotVariance => StoredSnapshotBalance - ClosingBalance;
    public List<CashBankLedgerLineDto> Lines { get; set; } = new();
}

public class CashBankLedgerLineDto
{
    public Guid TransactionId { get; set; }
    public Guid JournalEntryId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string JournalEntryNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid? SourceDocumentId { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public string? SegmentString { get; set; }
}
