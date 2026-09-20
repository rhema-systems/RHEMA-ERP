using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Governs closing revaluation for one account/currency in one exact accounting book.
/// Absence of an explicit override means the account-book classification default applies.
/// </summary>
public sealed class AccountBookCurrencyPolicy : TenantEntity
{
    public Guid AccountAccountingBookId { get; set; }
    public Guid AccountCurrencyLinkId { get; set; }

    public bool? RevaluationOverride { get; set; }

    [MaxLength(500)]
    public string? OverrideReason { get; set; }

    public bool? PendingRevaluationOverride { get; set; }

    [MaxLength(500)]
    public string? PendingReason { get; set; }

    [Required, MaxLength(30)]
    public string LifecycleStatus { get; set; } = "Active";

    public Guid? WorkflowInstanceId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? RequestedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    [MaxLength(500)]
    public string? DecisionReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public AccountAccountingBook AccountAccountingBook { get; set; } = null!;
    public AccountCurrencyLink AccountCurrencyLink { get; set; } = null!;
}
