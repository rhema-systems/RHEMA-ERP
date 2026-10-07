using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

#region BankReconciliation DTOs

public class BankReconciliationDto
{
    public Guid Id { get; set; }
    public Guid BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public DateTime ReconciliationDate { get; set; }
    public Guid? StatementId { get; set; }
    public decimal StatementBalance { get; set; }
    public decimal BookBalance { get; set; }
    public decimal Difference { get; set; }
    public ReconciliationStatus Status { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedBookCount { get; set; }
    public int UnmatchedStatementCount { get; set; }
    public Guid? ReconciledBy { get; set; }
    public string? ReconciledByName { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StartReconciliationDto
{
    public Guid BankAccountId { get; set; }
    public DateTime ReconciliationDate { get; set; }
    public decimal StatementBalance { get; set; }
    public Guid? StatementId { get; set; }
    public string? Notes { get; set; }
}

public class ReconciliationMatchDto
{
    public Guid Id { get; set; }
    public Guid ReconciliationId { get; set; }
    public Guid CashTransactionId { get; set; }
    public Guid BankStatementLineId { get; set; }
    public bool IsAutoMatched { get; set; }
    public int? MatchConfidence { get; set; }
    public DateTime MatchedAt { get; set; }
    public string? Notes { get; set; }
    public string CashTransactionNumber { get; set; } = string.Empty;
    public DateTime CashTransactionDate { get; set; }
    public CashTransactionType CashTransactionType { get; set; }
    public string CashTransactionDescription { get; set; } = string.Empty;
    public string? CashTransactionReference { get; set; }
    public decimal CashTransactionAmount { get; set; }
    public DateTime StatementTransactionDate { get; set; }
    public string StatementDescription { get; set; } = string.Empty;
    public string? StatementReference { get; set; }
    public decimal StatementDebitAmount { get; set; }
    public decimal StatementCreditAmount { get; set; }
}

public class CreateManualMatchDto
{
    public Guid ReconciliationId { get; set; }
    public Guid CashTransactionId { get; set; }
    public Guid BankStatementLineId { get; set; }
    public string? Notes { get; set; }
}

public class CreateReconciliationAdjustmentDto
{
    public ReconciliationAdjustmentType AdjustmentType { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public Guid OffsetAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class ReconciliationAdjustmentDto
{
    public Guid ReconciliationId { get; set; }
    public Guid CashTransactionId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public ReconciliationAdjustmentType AdjustmentType { get; set; }
    public CashTransactionType CashTransactionType { get; set; }
    public decimal Amount { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid OffsetAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public bool WasDuplicate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public string? ExchangeRateSource { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public string? ExchangeRateQuoteSide { get; set; }
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
}

public class CancelReconciliationDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ReturnReconciliationForCorrectionDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ReconciliationSummaryDto
{
    public Guid ReconciliationId { get; set; }
    public decimal StatementBalance { get; set; }
    public decimal BookBalance { get; set; }
    public decimal Difference { get; set; }
    public int TotalMatches { get; set; }
    public int AutoMatches { get; set; }
    public int ManualMatches { get; set; }
    public List<UnmatchedTransactionDto> UnmatchedBookTransactions { get; set; } = new();
    public List<UnmatchedStatementLineDto> UnmatchedStatementLines { get; set; } = new();
}

public class UnmatchedTransactionDto
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public CashTransactionType TransactionType { get; set; }
}

public class UnmatchedStatementLineDto
{
    public Guid Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
}

#endregion
