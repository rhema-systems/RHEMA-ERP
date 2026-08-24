using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Durable budget commitment created when a controlled source document enters approval.
/// It prevents concurrent documents from spending the same adopted Finance budget.
/// </summary>
public sealed class FinanceBudgetReservation : TenantEntity
{
    public Guid BudgetScenarioId { get; set; }
    public Guid BudgetReturnId { get; set; }
    public Guid BudgetEntryId { get; set; }
    public Guid AccountId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public Guid? SegmentValueId { get; set; }
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }

    /// <summary>
    /// Human-readable producer reference retained as evidence. Finance never uses this
    /// value as authority; the stable source ID and validated budget cell remain canonical.
    /// </summary>
    [MaxLength(100)]
    public string? SourceDocumentReference { get; set; }

    /// <summary>
    /// Producer-owned version or immutable evidence hash used to detect a changed document.
    /// </summary>
    [MaxLength(64)]
    public string? SourceVersion { get; set; }

    public DateTime BudgetDate { get; set; }

    /// <summary>
    /// Stable producer line IDs included in this budget-cell reservation. Several source
    /// lines may aggregate into one Finance budget cell, so the evidence is stored as JSON.
    /// </summary>
    [MaxLength(2000)]
    public string? SourceLineIdsJson { get; set; }

    [Required, MaxLength(3)]
    public string TransactionCurrencyCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransactionAmount { get; set; }

    public Guid? ExchangeRateId { get; set; }

    /// <summary>Transaction-currency to functional-currency multiplier.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1m;

    public int ReservationVersion { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAmountSnapshot { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal PostedActualSnapshot { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal OtherReservationsSnapshot { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal AvailableBeforeReservationSnapshot { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Reserved";
    [Required, MaxLength(64)]
    public string EvaluationHash { get; set; } = string.Empty;
    public Guid? OverrideRequestId { get; set; }
    public Guid ReservedByUserId { get; set; }
    public DateTime ReservedAt { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public DateTime? ReleasedAt { get; set; }
    [MaxLength(500)]
    public string? ReleaseReason { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Immutable idempotency and audit evidence for every generic Finance budget-reservation
/// state transition. Producer modules never write this table directly.
/// </summary>
public sealed class FinanceBudgetReservationOperation : TenantEntity
{
    public Guid? FinanceBudgetReservationId { get; set; }

    [MaxLength(2000)]
    public string? ReservationIdsJson { get; set; }

    [Required, MaxLength(30)]
    public string OperationType { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string PayloadHash { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PriorStatus { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string ResultStatus { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PriorReservedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ResultReservedAmount { get; set; }

    [Required, MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    public Guid ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }

    public FinanceBudgetReservation? FinanceBudgetReservation { get; set; }
}

/// <summary>
/// Immutable request to exceed a precise evaluated budget position. Approval applies only
/// to the captured evaluation hash; editing the journal invalidates the evidence.
/// </summary>
public sealed class FinanceBudgetOverrideRequest : TenantEntity
{
    [Required, MaxLength(50)]
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
    [Required, MaxLength(64)]
    public string EvaluationHash { get; set; } = string.Empty;
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")]
    public decimal RequestedAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ShortfallAmount { get; set; }
    [Required, MaxLength(30)]
    public string Status { get; set; } = "PendingApproval";
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
