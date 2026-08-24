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
