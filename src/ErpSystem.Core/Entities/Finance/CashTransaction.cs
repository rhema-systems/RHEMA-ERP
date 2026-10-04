using ErpSystem.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a cash transaction (receipt, payment, or transfer)
/// </summary>
public class CashTransaction : BaseEntity
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(50)]
    public string TransactionNumber { get; set; } = string.Empty;

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public CashTransactionType TransactionType { get; set; }

    [Required]
    public Guid BankAccountId { get; set; }

    /// <summary>
    /// For transfers, the destination bank account
    /// </summary>
    public Guid? ToBankAccountId { get; set; }

    /// <summary>
    /// Stable idempotency and lineage key shared by the OUT and IN rows of one bank transfer.
    /// It replaces document-number parsing as the primary way to locate the paired leg while
    /// leaving the human-readable OUT/IN suffix available to Finance users.
    /// </summary>
    public Guid? TransferPairId { get; set; }

    /// <summary>
    /// The bank-account side represented by this transfer row. Non-transfer transactions leave
    /// the value null because they do not participate in a paired movement.
    /// </summary>
    public BankTransferLeg? TransferLeg { get; set; }

    public decimal Amount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(20,6)")]
    public decimal RoundingAdjustmentAmount { get; set; }
    public Guid? FinanceRoundingEvidenceId { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>
    /// Exchange rate if different from base currency
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Immutable tenant exchange-rate record selected for this bank leg. Retaining the record id,
    /// source, date and quote side lets the posting engine and later auditors reproduce the exact
    /// functional-currency valuation instead of depending on whatever rate is current later.
    /// </summary>
    public Guid? ExchangeRateId { get; set; }

    [MaxLength(100)]
    public string? ExchangeRateSource { get; set; }

    public DateTime? ExchangeRateDate { get; set; }

    public ExchangeRateQuoteSide? ExchangeRateQuoteSide { get; set; }

    /// <summary>
    /// Amount in base currency (GHS)
    /// </summary>
    public decimal BaseAmount { get; set; }

    /// <summary>
    /// Destination-currency units received for one source-currency unit. Both legs retain the
    /// same cross-rate snapshot so either bank statement can explain the conversion.
    /// </summary>
    public decimal? TransferCrossRate { get; set; }

    /// <summary>
    /// Signed functional-currency difference between the destination and source valuations.
    /// Positive is a realised gain; negative is a realised loss. The amount is informational on
    /// both operational legs and is posted exactly once by the source leg's journal.
    /// </summary>
    public decimal TransferFxGainLossBaseAmount { get; set; }

    public Guid? PaymentMethodId { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(200)]
    public string? PayeeOrPayer { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// GL Account to post against (e.g., Sales, Purchases, Expenses)
    /// </summary>
    public Guid? GLAccountId { get; set; }

    /// <summary>
    /// Whether this transaction has been reconciled with bank statement
    /// </summary>
    public bool IsReconciled { get; set; }

    public Guid? ReconciliationId { get; set; }

    /// <summary>
    /// If payment was by cheque
    /// </summary>
    public Guid? ChequeId { get; set; }

    /// <summary>
    /// Whether transaction has been posted to GL
    /// </summary>
    public bool IsPosted { get; set; }

    public CashTransactionApprovalStatus ApprovalStatus { get; set; } = CashTransactionApprovalStatus.Captured;

    public Guid? WorkflowInstanceId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? SubmittedById { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedById { get; set; }

    public DateTime? RejectedAt { get; set; }

    public Guid? RejectedById { get; set; }

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public Guid? CancelledById { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    public Guid? JournalEntryId { get; set; }
    public Guid? SourceBookAuthorityId { get; set; }
    public virtual FinanceSourceBookAuthority? SourceBookAuthority { get; set; }

    public DateTime? PostedDate { get; set; }

    public Guid? PostedBy { get; set; }

    /// <summary>
    /// True when this posted operational row has been corrected by a controlled compensating
    /// transaction. The original row remains posted and immutable because changing it would
    /// break both the bank-reconciliation trail and the source-to-ledger evidence.
    /// </summary>
    public bool IsReversed { get; set; }

    /// <summary>
    /// Populated on a compensating cash row to identify the original operational movement it
    /// offsets. Transfer reversals use one link per bank leg so each account has a complete trail.
    /// </summary>
    public Guid? ReversalOfCashTransactionId { get; set; }

    /// <summary>
    /// Populated on an original cash row with the compensating operational row. This makes the
    /// correction idempotent and lets reconciliation reviewers navigate in both directions.
    /// </summary>
    public Guid? ReversalCashTransactionId { get; set; }

    /// <summary>
    /// Journal and posting-event lineage for the compensating GL entry. For a transfer both
    /// original legs share the same reversal journal and posting event.
    /// </summary>
    public Guid? ReversalJournalEntryId { get; set; }

    public Guid? ReversalPostingEventId { get; set; }

    public DateTime? ReversalDate { get; set; }

    public DateTime? ReversedAt { get; set; }

    public Guid? ReversedById { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    // Navigation properties
    public virtual BankAccount BankAccount { get; set; } = null!;
    public virtual BankAccount? ToBankAccount { get; set; }
    public virtual PaymentMethod? PaymentMethod { get; set; }
    public virtual Cheque? Cheque { get; set; }
    public virtual BankReconciliation? Reconciliation { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual ExchangeRate? ExchangeRateRecord { get; set; }

    // Explicit self-references preserve immutable operational lineage without relying on
    // transaction-number conventions when a reviewer follows a correction.
    public virtual CashTransaction? ReversalOfCashTransaction { get; set; }
    public virtual CashTransaction? ReversalCashTransaction { get; set; }
    public virtual JournalEntry? ReversalJournalEntry { get; set; }
    public virtual FinancePostingEvent? ReversalPostingEvent { get; set; }
}
