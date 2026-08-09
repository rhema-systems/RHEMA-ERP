using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Finance.FixedAssets;

/// <summary>
/// Maker-checker evidence for correcting an incorrectly posted fixed-asset valuation.
/// The original valuation and journal remain immutable; this record links them to the
/// compensating journal and the exact book snapshot restored after posting.
/// </summary>
public sealed class AssetValuationCorrection : TenantEntity
{
    [Required]
    public Guid OriginalValuationId { get; set; }
    public AssetValuation OriginalValuation { get; set; } = null!;

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
    public string Status { get; set; } = AssetValuationCorrectionStatuses.PendingApproval;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Explains affected reports, depreciation, disposal work, and any required repost. Keeping
    /// this separate from the reason gives the checker enough evidence to make an informed decision.
    /// </summary>
    [Required, MaxLength(2000)]
    public string ImpactAssessment { get; set; } = string.Empty;

    public DateTime RequestedReversalDate { get; set; }
    public Guid RequestedByUserId { get; set; }
    [Required, MaxLength(200)] public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedByUserId { get; set; }
    [MaxLength(200)] public string? ReviewedByUserName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(2000)] public string? ReviewComment { get; set; }

    public DateTime? PostedAt { get; set; }
    [MaxLength(2000)] public string? FailureReason { get; set; }
}

public static class AssetValuationCorrectionStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Posted = "Posted";
}
