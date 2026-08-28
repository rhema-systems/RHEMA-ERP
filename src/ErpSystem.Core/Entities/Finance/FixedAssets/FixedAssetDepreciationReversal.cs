using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Finance.FixedAssets;

/// <summary>
/// Stores the independent approval and immutable posting lineage used to reverse one posted
/// depreciation run. The original run and schedules remain posted accounting evidence; this
/// record links them to the compensating journal and allows a corrected revision to be run.
/// </summary>
public sealed class FixedAssetDepreciationReversal : TenantEntity
{
    [Required]
    public Guid OriginalDepreciationRunId { get; set; }
    public FixedAssetDepreciationRun OriginalDepreciationRun { get; set; } = null!;

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
    public string Status { get; set; } = FixedAssetDepreciationReversalStatuses.PendingApproval;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Records the maker's assessment of affected assets, reports, periods, and downstream work.
    /// A separate narrative helps the reviewer understand why reversal is safe before approval.
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

public static class FixedAssetDepreciationReversalStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Posted = "Posted";
    public const string Failed = "Failed";
}
