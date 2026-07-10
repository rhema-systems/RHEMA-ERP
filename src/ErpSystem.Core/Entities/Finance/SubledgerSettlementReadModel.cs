using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

public static class SubledgerSettlementModules
{
    public const string AccountsPayable = "AP";
    public const string AccountsReceivable = "AR";
}

public static class SubledgerSettlementStatuses
{
    public const string Open = "Open";
    public const string PartiallySettled = "PartiallySettled";
    public const string Settled = "Settled";
    public const string OverSettled = "OverSettled";
    public const string Disputed = "Disputed";
}

public class SubledgerSettlementBalance : BusinessEntity
{
    [Required]
    [MaxLength(10)]
    public string SourceModule { get; set; } = SubledgerSettlementModules.AccountsReceivable;

    [Required]
    public Guid CounterpartyId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SourceDocumentType { get; set; } = string.Empty;

    [Required]
    public Guid SourceDocumentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SourceDocumentNumber { get; set; } = string.Empty;

    public Guid? SourcePostingEventId { get; set; }

    public Guid? SourceJournalEntryId { get; set; }

    public DateTime TransactionDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Required]
    [MaxLength(3)]
    public string DocumentCurrencyCode { get; set; } = "GHS";

    [Required]
    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalDocumentAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SettledAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithheldAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingAmount { get; set; }

    [Required]
    [MaxLength(30)]
    public string SettlementStatus { get; set; } = SubledgerSettlementStatuses.Open;

    public Guid RebuildBatchId { get; set; }

    public DateTime LastRebuiltAt { get; set; }

    public bool HasDiagnostics { get; set; }

    [MaxLength(1000)]
    public string? DiagnosticFlags { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OperationalPaidAmountSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OperationalCreditedAmountSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OperationalOutstandingSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OperationalVariance { get; set; }

    public virtual FinancePostingEvent? SourcePostingEvent { get; set; }
    public virtual JournalEntry? SourceJournalEntry { get; set; }
    public virtual ICollection<SubledgerSettlementApplication> Applications { get; set; } = new List<SubledgerSettlementApplication>();
}

public class SubledgerSettlementApplication : BusinessEntity
{
    [Required]
    public Guid SubledgerSettlementBalanceId { get; set; }

    [Required]
    [MaxLength(10)]
    public string SourceModule { get; set; } = SubledgerSettlementModules.AccountsReceivable;

    [Required]
    public Guid CounterpartyId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SourceDocumentType { get; set; } = string.Empty;

    [Required]
    public Guid SourceDocumentId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SettlementSourceType { get; set; } = string.Empty;

    public Guid SettlementSourceId { get; set; }

    public Guid? SettlementAllocationId { get; set; }

    public Guid? SettlementPostingEventId { get; set; }

    public Guid? SettlementJournalEntryId { get; set; }

    public DateTime SettlementDate { get; set; }

    [Required]
    [MaxLength(3)]
    public string DocumentCurrencyCode { get; set; } = "GHS";

    [Required]
    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,2)")]
    public decimal SettledAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithheldAmount { get; set; }

    public Guid? FxRealizedSettlementId { get; set; }

    public Guid RebuildBatchId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual SubledgerSettlementBalance Balance { get; set; } = null!;
    public virtual FinancePostingEvent? SettlementPostingEvent { get; set; }
    public virtual JournalEntry? SettlementJournalEntry { get; set; }
    public virtual FxRealizedSettlement? FxRealizedSettlement { get; set; }
}
