using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Journal entry status for unit journal entries.
/// </summary>
public enum UnitJournalEntryStatus
{
    /// <summary>Entry is being prepared, not yet submitted.</summary>
    Draft = 0,
    
    /// <summary>Entry submitted and awaiting approval.</summary>
    PendingApproval = 1,
    
    /// <summary>Entry approved but not yet posted.</summary>
    Approved = 2,
    
    /// <summary>Entry rejected during approval.</summary>
    Rejected = 3,
    
    /// <summary>Entry posted and balances updated.</summary>
    Posted = 4,
    
    /// <summary>Entry has been reversed.</summary>
    Reversed = 5,

    /// <summary>Validated submission; no active approval process was configured.</summary>
    ReadyToPost = 6
}

/// <summary>
/// Represents a journal entry for posting unit quantities to unit accounts.
/// Similar to financial JournalEntry but for non-financial quantities.
/// Supports approval workflow (Draft → PendingApproval → Approved → Posted).
/// </summary>
public class UnitJournalEntry : TenantEntity
{
    // ========================================================================
    // ENTRY IDENTIFICATION
    // ========================================================================

    /// <summary>
    /// System-generated entry number (sequential).
    /// Format: UJE-YYYY-NNNNN (e.g., UJE-2025-00001)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EntryNumber { get; set; } = string.Empty;

    /// <summary>
    /// Date of the entry (transaction date).
    /// Must fall within an open fiscal period.
    /// </summary>
    [Required]
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Description/narrative for the journal entry.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    // ========================================================================
    // FISCAL PERIOD TRACKING
    // ========================================================================

    /// <summary>
    /// Foreign key to the fiscal year.
    /// </summary>
    [Required]
    public Guid FiscalYearId { get; set; }

    /// <summary>
    /// Navigation property to fiscal year.
    /// </summary>
    [ForeignKey(nameof(FiscalYearId))]
    public virtual FiscalYear? FiscalYear { get; set; }

    /// <summary>
    /// Foreign key to the fiscal period.
    /// </summary>
    [Required]
    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// Navigation property to fiscal period.
    /// </summary>
    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod? FiscalPeriod { get; set; }

    // ========================================================================
    // WORKFLOW STATUS
    // ========================================================================

    /// <summary>
    /// Current status of the entry in the workflow.
    /// </summary>
    [Required]
    public UnitJournalEntryStatus Status { get; set; } = UnitJournalEntryStatus.Draft;
    public bool ApprovalRequired { get; set; } = true;
    public Guid? WorkflowInstanceId { get; set; }

    /// <summary>
    /// Reference to source document (e.g., "HR Report #123").
    /// </summary>
    [MaxLength(200)]
    public string? SourceDocument { get; set; }

    // ========================================================================
    // APPROVAL TRACKING
    // ========================================================================

    /// <summary>
    /// When the entry was approved.
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// User ID who approved the entry.
    /// </summary>
    public Guid? ApprovedBy { get; set; }

    /// <summary>
    /// Username/name of approver for display purposes.
    /// </summary>
    [MaxLength(100)]
    public string? ApprovedByName { get; set; }

    /// <summary>
    /// Reason for rejection if status is Rejected.
    /// </summary>
    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    // ========================================================================
    // POSTING TRACKING
    // ========================================================================

    /// <summary>
    /// When the entry was posted.
    /// </summary>
    public DateTime? PostedAt { get; set; }

    /// <summary>
    /// User ID who posted the entry.
    /// </summary>
    public Guid? PostedBy { get; set; }

    /// <summary>
    /// Username/name of poster for display purposes.
    /// </summary>
    [MaxLength(100)]
    public string? PostedByName { get; set; }

    // ========================================================================
    // REVERSAL TRACKING
    // ========================================================================

    /// <summary>
    /// Whether this entry is a reversal of another entry.
    /// </summary>
    public bool IsReversal { get; set; } = false;

    /// <summary>
    /// ID of the original entry if this is a reversal.
    /// </summary>
    public Guid? ReversedEntryId { get; set; }

    /// <summary>
    /// ID of the reversal entry if this entry was reversed.
    /// </summary>
    public Guid? ReversalEntryId { get; set; }

    /// <summary>
    /// Reason for reversal.
    /// </summary>
    [MaxLength(500)]
    public string? ReversalReason { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    /// <summary>
    /// Lines/details of this journal entry.
    /// </summary>
    public virtual ICollection<UnitJournalEntryLine> Lines { get; set; } = new List<UnitJournalEntryLine>();
}
