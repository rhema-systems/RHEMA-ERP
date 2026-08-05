using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

public class FxRealizedSettlement : TenantEntity
{
    [Required]
    [MaxLength(20)]
    public string SourceModule { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SettlementDocumentType { get; set; } = string.Empty;

    public Guid SettlementDocumentId { get; set; }

    public Guid SettlementAllocationId { get; set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceDocumentType { get; set; } = string.Empty;

    public Guid InvoiceDocumentId { get; set; }

    public Guid ControlAccountId { get; set; }

    [Required]
    [MaxLength(3)]
    public string TransactionCurrency { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SettledForeignAmount { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal HistoricalExchangeRate { get; set; }

    public Guid? HistoricalExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal SettlementExchangeRate { get; set; }

    public Guid? SettlementExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HistoricalFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SettlementFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GainLossAmount { get; set; }

    [Required]
    [MaxLength(10)]
    public string GainLossType { get; set; } = string.Empty;

    public Guid GainLossAccountId { get; set; }

    public Guid? JournalEntryId { get; set; }

    public Guid? PostingEventId { get; set; }

    /// <summary>
    /// Realized FX is a separate accounting event from the settlement journal. A source-payment
    /// reversal must therefore reverse and link this event as well; otherwise the gain/loss would
    /// remain in the ledger after the underlying settlement was removed.
    /// </summary>
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public DateTime? ReversedAt { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Posted";

    [MaxLength(450)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime SettlementDate { get; set; }

    public DateTime PostedAt { get; set; }

    [ForeignKey(nameof(ControlAccountId))]
    public virtual Account ControlAccount { get; set; } = null!;

    [ForeignKey(nameof(GainLossAccountId))]
    public virtual Account GainLossAccount { get; set; } = null!;

    [ForeignKey(nameof(JournalEntryId))]
    public virtual JournalEntry? JournalEntry { get; set; }

    [ForeignKey(nameof(PostingEventId))]
    public virtual FinancePostingEvent? PostingEvent { get; set; }
}

public class FxRevaluationBatch : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    public DateTime RevaluationDate { get; set; }

    public Guid FiscalPeriodId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Draft";

    [Required]
    [MaxLength(30)]
    public string Scope { get; set; } = "Combined";

    [Required]
    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalGainAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalLossAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetGainLossAmount { get; set; }

    public bool AutoReverseNextPeriod { get; set; }

    public Guid? JournalEntryId { get; set; }

    public Guid? PostingEventId { get; set; }

    public Guid? ReversalJournalEntryId { get; set; }

    public Guid? ReversalPostingEventId { get; set; }

    [MaxLength(450)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime? PostedAt { get; set; }

    public DateTime? ReversedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual ICollection<FxRevaluationLine> Lines { get; set; } = new List<FxRevaluationLine>();

    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;

    [ForeignKey(nameof(JournalEntryId))]
    public virtual JournalEntry? JournalEntry { get; set; }

    [ForeignKey(nameof(PostingEventId))]
    public virtual FinancePostingEvent? PostingEvent { get; set; }

    [ForeignKey(nameof(ReversalJournalEntryId))]
    public virtual JournalEntry? ReversalJournalEntry { get; set; }

    [ForeignKey(nameof(ReversalPostingEventId))]
    public virtual FinancePostingEvent? ReversalPostingEvent { get; set; }
}

public class FxRevaluationLine : TenantEntity
{
    public Guid FxRevaluationBatchId { get; set; }

    [Required]
    [MaxLength(20)]
    public string SourceModule { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? SourceDocumentType { get; set; }

    public Guid? SourceDocumentId { get; set; }

    public Guid AccountId { get; set; }

    [Required]
    [MaxLength(3)]
    public string TransactionCurrency { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ForeignCurrencyBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CarryingFunctionalAmount { get; set; }

    public Guid ClosingExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal ClosingExchangeRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RevaluedFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GainLossAmount { get; set; }

    [Required]
    [MaxLength(10)]
    public string GainLossType { get; set; } = string.Empty;

    public Guid GainLossAccountId { get; set; }

    public Guid? JournalEntryId { get; set; }

    public Guid? PostingEventId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(FxRevaluationBatchId))]
    public virtual FxRevaluationBatch Batch { get; set; } = null!;

    [ForeignKey(nameof(AccountId))]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey(nameof(ClosingExchangeRateId))]
    public virtual ExchangeRate ClosingExchangeRateRecord { get; set; } = null!;

    [ForeignKey(nameof(GainLossAccountId))]
    public virtual Account GainLossAccount { get; set; } = null!;

    [ForeignKey(nameof(JournalEntryId))]
    public virtual JournalEntry? JournalEntry { get; set; }

    [ForeignKey(nameof(PostingEventId))]
    public virtual FinancePostingEvent? PostingEvent { get; set; }
}
