using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Immutable maker-checker evidence for one numeric exchange-rate exception on one source
/// transaction and transaction currency. It never changes the governed exchange-rate master.
/// </summary>
public sealed class FinanceExchangeRateOverrideRequest : TenantEntity
{
    [Required, MaxLength(100)]
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    [MaxLength(150)] public string? SourceDocumentReference { get; set; }
    [Required, MaxLength(3)] public string TransactionCurrencyCode { get; set; } = string.Empty;
    [Required, MaxLength(3)] public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public Guid GovernedExchangeRateId { get; set; }
    public ExchangeRate GovernedExchangeRate { get; set; } = null!;
    [Column(TypeName = "decimal(18,6)")] public decimal GovernedRate { get; set; }
    [Required, MaxLength(100)] public string GovernedRateSource { get; set; } = string.Empty;
    public DateTime GovernedRateEffectiveDate { get; set; }
    [Required, MaxLength(30)] public string GovernedRateType { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string GovernedQuoteSide { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,6)")] public decimal RequestedRate { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string SourceSnapshotHash { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Status { get; set; } = FinanceExchangeRateOverrideStatuses.PendingApproval;
    public Guid? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    [MaxLength(1000)] public string? RejectionReason { get; set; }
    public Guid? SupersededByRequestId { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    [MaxLength(1000)] public string? SupersessionReason { get; set; }
    public Guid? ConsumedByPostingEventId { get; set; }
    public FinancePostingEvent? ConsumedByPostingEvent { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public static class FinanceExchangeRateOverrideStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Superseded = "Superseded";
    public const string Consumed = "Consumed";
}
