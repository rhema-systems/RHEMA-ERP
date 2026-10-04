using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

#region CashTransaction DTOs

public class CashTransactionDto
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public CashTransactionType TransactionType { get; set; }
    public Guid BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public Guid? ToBankAccountId { get; set; }
    public string? ToBankAccountName { get; set; }
    public Guid? TransferPairId { get; set; }
    public BankTransferLeg? TransferLeg { get; set; }
    public decimal Amount { get; set; }
    public decimal RoundingAdjustmentAmount { get; set; }
    public Guid? FinanceRoundingEvidenceId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public string? ExchangeRateSource { get; set; }
    public DateTime? ExchangeRateDate { get; set; }
    public string? ExchangeRateQuoteSide { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal? TransferCrossRate { get; set; }
    public decimal TransferFxGainLossBaseAmount { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? PayeeOrPayer { get; set; }
    public string? Description { get; set; }
    public Guid? GLAccountId { get; set; }
    public string? GLAccountNumber { get; set; }
    public string? GLAccountName { get; set; }
    public bool IsReconciled { get; set; }
    public Guid? ReconciliationId { get; set; }
    public Guid? ChequeId { get; set; }
    public string? ChequeNumber { get; set; }
    public bool IsPosted { get; set; }
    public CashTransactionApprovalStatus ApprovalStatus { get; set; }
    public string ApprovalStatusName { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedById { get; set; }
    public string? ApprovalComments { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedDate { get; set; }
    public bool IsReversed { get; set; }
    public Guid? ReversalOfCashTransactionId { get; set; }
    public Guid? ReversalCashTransactionId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public DateTime? ReversalDate { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }
    public string? ReversalReason { get; set; }
    public ControlledDocumentIssueSummaryDto? PaymentSlipIssuance { get; set; }
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateCashReceiptDto
{
    public DateTime TransactionDate { get; set; }
    public Guid BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? PayerName { get; set; }
    public string? Description { get; set; }
    public Guid? GLAccountId { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class CreateCashPaymentDto
{
    public DateTime TransactionDate { get; set; }
    public Guid BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? PayeeName { get; set; }
    public string? Description { get; set; }
    public Guid? GLAccountId { get; set; }
    public Guid? ChequeId { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class CreateBankTransferDto
{
    /// <summary>
    /// Client-generated retry key. Reusing the same key with the same transfer returns the
    /// existing pair instead of consuming another document number or moving funds twice.
    /// </summary>
    public Guid? TransferPairId { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid FromBankAccountId { get; set; }
    public Guid ToBankAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal? DestinationAmount { get; set; }
    public Guid? SourceExchangeRateId { get; set; }
    public Guid? DestinationExchangeRateId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

/// <summary>
/// Server-owned valuation preview for a same- or cross-currency bank transfer. The create command
/// uses the same resolver, so the screen can explain the exact amounts and realised FX posting
/// before Finance submits the transaction.
/// </summary>
public sealed class BankTransferPreviewDto
{
    public Guid FromBankAccountId { get; set; }
    public string FromBankAccountName { get; set; } = string.Empty;
    public Guid ToBankAccountId { get; set; }
    public string ToBankAccountName { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public bool IsCrossCurrency { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal SourceAmount { get; set; }
    public decimal SourceExchangeRate { get; set; }
    public Guid? SourceExchangeRateId { get; set; }
    public string SourceExchangeRateSource { get; set; } = string.Empty;
    public DateTime SourceExchangeRateDate { get; set; }
    public string SourceExchangeRateQuoteSide { get; set; } = string.Empty;
    public decimal SourceBaseAmount { get; set; }
    public string DestinationCurrency { get; set; } = string.Empty;
    public decimal DestinationAmount { get; set; }
    public bool DestinationAmountWasDerived { get; set; }
    public decimal DestinationExchangeRate { get; set; }
    public Guid? DestinationExchangeRateId { get; set; }
    public string DestinationExchangeRateSource { get; set; } = string.Empty;
    public DateTime DestinationExchangeRateDate { get; set; }
    public string DestinationExchangeRateQuoteSide { get; set; } = string.Empty;
    public decimal DestinationBaseAmount { get; set; }
    public decimal CrossRate { get; set; }
    public decimal RealizedFxGainLossBaseAmount { get; set; }
    public string RealizedFxOutcome { get; set; } = "None";
    public string FunctionalCurrency { get; set; } = string.Empty;
}

public class CashTransactionWorkflowActionDto
{
    public string? Comments { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Requests a controlled correction of a posted, Finance-owned cash receipt, cash payment, or
/// bank transfer. The date remains optional because the tenant reversal policy selects the
/// appropriate open period when Finance does not explicitly provide one.
/// </summary>
public sealed class ReverseCashTransactionDto
{
    public string Reason { get; set; } = string.Empty;
    public DateTime? ReversalDate { get; set; }
}

/// <summary>
/// Complete audit view of a cash/bank source row, its paired or compensating operational rows,
/// central posting events, journal lines, and Finance audit events.
/// </summary>
public sealed class CashTransactionTraceDto
{
    public CashTransactionDto Transaction { get; set; } = new();
    public List<CashTransactionDto> RelatedTransactions { get; set; } = new();
    public List<FinancePostingTraceDto> Postings { get; set; } = new();
    public List<FinanceAuditTraceDto> AuditEvents { get; set; } = new();
}

#endregion
