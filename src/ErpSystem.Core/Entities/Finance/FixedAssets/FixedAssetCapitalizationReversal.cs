using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Finance.FixedAssets;

/// <summary>
/// Persists the maker-checker decision and posting lineage for one fixed-asset capitalization
/// reversal. Posted asset cost is never edited in place: the original Finance event remains the
/// accounting evidence and an approved compensating journal supplies the correction required by
/// TDC requirements FR-GL-008 and FR-GL-010 (FIN-LIM-0030).
/// </summary>
public sealed class FixedAssetCapitalizationReversal : TenantEntity
{
    [Required]
    public Guid FixedAssetId { get; set; }
    public FixedAsset FixedAsset { get; set; } = null!;

    [Required]
    public Guid OriginalPostingEventId { get; set; }
    public FinancePostingEvent OriginalPostingEvent { get; set; } = null!;

    [Required]
    public Guid OriginalJournalEntryId { get; set; }
    public JournalEntry OriginalJournalEntry { get; set; } = null!;

    public Guid? ReversalPostingEventId { get; set; }
    public FinancePostingEvent? ReversalPostingEvent { get; set; }

    public Guid? ReversalJournalEntryId { get; set; }
    public JournalEntry? ReversalJournalEntry { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = FixedAssetCapitalizationReversalStatuses.PendingApproval;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable impact assessment captured by the maker. Keeping this separate from the
    /// short reason makes the approval record useful to Finance reviewers and external auditors.
    /// </summary>
    [Required, MaxLength(2000)]
    public string ImpactAssessment { get; set; } = string.Empty;

    public DateTime RequestedReversalDate { get; set; }
    public Guid RequestedByUserId { get; set; }

    [Required, MaxLength(200)]
    public string RequestedByUserName { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
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

/// <summary>
/// Explicit lifecycle states prevent an approved request from being confused with a posted
/// correction. This distinction is important when an accounting period closes between review
/// and posting: the request remains approved but the posting engine must reject the stale date.
/// </summary>
public static class FixedAssetCapitalizationReversalStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Posted = "Posted";
    public const string Failed = "Failed";
}
