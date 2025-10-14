using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents an approval request for a workflow step
/// </summary>
[Table("WorkflowApprovals")]
public class WorkflowApproval : TenantEntity
{

    /// <summary>
    /// ID of the step instance this approval is for
    /// </summary>
    [Required]
    public Guid StepInstanceId { get; set; }

    /// <summary>
    /// ID of the user who should approve (optional if role-based)
    /// </summary>
    public Guid? ApproverId { get; set; }

    /// <summary>
    /// Role that should approve (optional if user-based)
    /// </summary>
    [StringLength(100)]
    public string? ApproverRole { get; set; }

    /// <summary>
    /// Current status of the approval
    /// </summary>
    [Required]
    public WorkflowApprovalStatus Status { get; set; } = WorkflowApprovalStatus.Pending;

    /// <summary>
    /// Date when the approval was requested
    /// </summary>
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the approval was processed
    /// </summary>
    public DateTime? ProcessedDate { get; set; }

    /// <summary>
    /// Due date for the approval
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Comments from the approver
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Comments { get; set; }

    /// <summary>
    /// ID of the user who processed the approval (in case of delegation)
    /// </summary>
    public Guid? ProcessedById { get; set; }

    /// <summary>
    /// Priority of this approval
    /// </summary>
    public WorkflowPriority Priority { get; set; } = WorkflowPriority.Normal;


    /// <summary>
    /// Navigation property for the step instance this approval is for
    /// </summary>
    [ForeignKey(nameof(StepInstanceId))]
    public virtual WorkflowStepInstance StepInstance { get; set; } = null!;

    /// <summary>
    /// Navigation property for the approver
    /// </summary>
    [ForeignKey(nameof(ApproverId))]
    public virtual ApplicationUser? Approver { get; set; }

    /// <summary>
    /// Navigation property for the user who processed the approval
    /// </summary>
    [ForeignKey(nameof(ProcessedById))]
    public virtual ApplicationUser? ProcessedBy { get; set; }

}