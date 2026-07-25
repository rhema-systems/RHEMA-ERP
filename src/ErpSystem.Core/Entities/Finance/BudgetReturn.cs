using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a "packet" or "worksheet" of budget entries assigned to a specific department or user.
/// This enables distributed budgeting where different users work on different slices of the budget.
/// </summary>
public class BudgetReturn : TenantEntity
{
    [Required]
    public Guid BudgetScenarioId { get; set; }

    [ForeignKey(nameof(BudgetScenarioId))]
    public virtual BudgetScenario? BudgetScenario { get; set; }

    /// <summary>
    /// The Department or Cost Center Segment Value ID this return applies to.
    /// Optional: If null, it might be a general budget return.
    /// </summary>
    public Guid? SegmentValueId { get; set; }

    [ForeignKey(nameof(SegmentValueId))]
    public virtual SegmentLookupValue? SegmentValue { get; set; }

    /// <summary>
    /// User assigned to prepare this budget return.
    /// </summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>
    /// User authorized to approve this budget return.
    /// </summary>
    public Guid? ApproverUserId { get; set; }

    /// <summary>
    /// Status of this specific return packet.
    /// Values: Draft, Submitted, Approved, Rejected.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public DateTime? SubmittedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }

    public virtual ICollection<BudgetEntry> BudgetEntries { get; set; } = new List<BudgetEntry>();
}
