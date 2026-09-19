using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

public class OpeningBalanceBatch : BusinessEntity
{
    [Required]
    [MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SourceReference { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime OpeningDate { get; set; }

    public Guid FiscalPeriodId { get; set; }
    public FiscalPeriod FiscalPeriod { get; set; } = null!;

    [MaxLength(30)]
    public string BookClassification { get; set; } = "IFRS";

    [MaxLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Difference { get; set; }

    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    public DateTime? ValidatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? FailedAt { get; set; }

    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    public ICollection<OpeningBalanceLine> Lines { get; set; } = new List<OpeningBalanceLine>();
    public ICollection<OpeningBalanceBatchReversal> Reversals { get; set; } = new List<OpeningBalanceBatchReversal>();
}

public sealed class OpeningBalanceBatchReversal : TenantEntity
{
    public Guid OpeningBalanceBatchId { get; set; }
    public OpeningBalanceBatch OpeningBalanceBatch { get; set; } = null!;
    public Guid OriginalPostingEventId { get; set; }
    public Guid OriginalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }

    [Required, MaxLength(40)]
    public string SourceKind { get; set; } = "FreeForm";

    [Required, MaxLength(30)]
    public string BookClassification { get; set; } = "IFRS";

    public DateTime OriginalOpeningDate { get; set; }
    public decimal OriginalTotalDebit { get; set; }
    public decimal OriginalTotalCredit { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = OpeningBalanceBatchReversalStatuses.PendingApproval;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string ImpactAssessment { get; set; } = string.Empty;

    public DateTime RequestedReversalDate { get; set; }
    public Guid RequestedByUserId { get; set; }

    [Required, MaxLength(200)]
    public string RequestedByUserName { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    [MaxLength(200)]
    public string? ReviewedByUserName { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(2000)]
    public string? ReviewComment { get; set; }

    public DateTime? PostedAt { get; set; }

    [MaxLength(2000)]
    public string? FailureReason { get; set; }
}

public static class OpeningBalanceBatchReversalStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Posted = "Posted";
    public const string Failed = "Failed";
}

public class OpeningBalanceLine : TenantEntity
{
    public Guid OpeningBalanceBatchId { get; set; }
    public OpeningBalanceBatch Batch { get; set; } = null!;

    public int LineNumber { get; set; }

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }

    /// <summary>
    /// Optional native-currency quantities. GL debit/credit remain functional amounts; these
    /// fields preserve the exact foreign advance lot that later allocation and FX accounting use.
    /// </summary>
    public decimal? TransactionDebitAmount { get; set; }
    public decimal? TransactionCreditAmount { get; set; }

    [MaxLength(3)]
    public string TransactionCurrencyCode { get; set; } = "GHS";

    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";

    public Guid? ExchangeRateId { get; set; }
    public ExchangeRate? ExchangeRate { get; set; }

    public DateTime? ExchangeRateDate { get; set; }

    [MaxLength(500)]
    public string? SegmentString { get; set; }

    public Guid? BankAccountId { get; set; }

    [MaxLength(30)]
    public string? CounterpartyType { get; set; }

    public Guid? CounterpartyId { get; set; }

    [MaxLength(100)]
    public string? SourceReference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
