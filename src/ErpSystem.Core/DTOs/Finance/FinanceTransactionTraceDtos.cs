namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Posting evidence shared by Finance source-document traces. The model is intentionally
/// source-neutral so AP, AR, cash/bank, and asset corrections expose the same ledger vocabulary.
/// </summary>
public sealed class FinancePostingTraceDto
{
    public Guid PostingEventId { get; set; }
    public string PostingAction { get; set; } = string.Empty;
    public string PostingStatus { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? JournalEntryNumber { get; set; }
    public Guid? OriginalJournalEntryId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public decimal TotalDebitAmount { get; set; }
    public decimal TotalCreditAmount { get; set; }
    public string FunctionalCurrencyCode { get; set; } = "GHS";
    public List<FinanceJournalLineTraceDto> Lines { get; set; } = new();
}

public sealed class FinanceJournalLineTraceDto
{
    public Guid TransactionId { get; set; }
    public int LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string TransactionCurrency { get; set; } = "GHS";
    public decimal? ForeignCurrencyAmount { get; set; }
    public decimal? ExchangeRate { get; set; }
    public Guid? OriginalTransactionId { get; set; }
    public Guid? ReversalTransactionId { get; set; }
}

public sealed class FinanceAuditTraceDto
{
    public Guid AuditLogId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? BeforeValuesJson { get; set; }
    public string? DetailsJson { get; set; }
}

/// <summary>
/// Finance operational-subledger evidence associated with a source transaction, such as a bank
/// cash transaction or liquidity holding-account entry.
/// </summary>
public sealed class FinanceOperationalTraceDto
{
    public string RecordType { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public Guid? OriginalRecordId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RecordDate { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool IsReconciled { get; set; }
}
