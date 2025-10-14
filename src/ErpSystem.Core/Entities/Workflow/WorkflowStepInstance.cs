using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents an instance of a workflow step being executed
/// </summary>
[Table("WorkflowStepInstances")]
public class WorkflowStepInstance : TenantEntity
{

    /// <summary>
    /// ID of the workflow instance this step belongs to
    /// </summary>
    [Required]
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// ID of the workflow step definition this instance is based on
    /// </summary>
    [Required]
    public Guid WorkflowStepId { get; set; }

    /// <summary>
    /// Alias for WorkflowStepId for compatibility
    /// </summary>
    public Guid StepId => WorkflowStepId;

    /// <summary>
    /// Current status of the step instance
    /// </summary>
    [Required]
    public WorkflowStepInstanceStatus Status { get; set; } = WorkflowStepInstanceStatus.Pending;

    /// <summary>
    /// ID of the user assigned to this step (if any)
    /// </summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>
    /// Date when the step instance was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the step instance was started
    /// </summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>
    /// Date when the step instance was completed
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Due date for completing this step
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// JSON data result from processing this step
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ResultData { get; set; }

    /// <summary>
    /// Comments or notes about this step execution
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Comments { get; set; }

    /// <summary>
    /// Number of retry attempts for this step
    /// </summary>
    public int RetryCount { get; set; } = 0;


    /// <summary>
    /// Navigation property for the workflow instance this step belongs to
    /// </summary>
    [ForeignKey(nameof(WorkflowInstanceId))]
    public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;

    /// <summary>
    /// Navigation property for the workflow step definition this instance is based on
    /// </summary>
    [ForeignKey(nameof(WorkflowStepId))]
    public virtual WorkflowStep WorkflowStep { get; set; } = null!;

    /// <summary>
    /// Navigation property for the user assigned to this step
    /// </summary>
    [ForeignKey(nameof(AssignedToId))]
    public virtual ApplicationUser? AssignedTo { get; set; }

    /// <summary>
    /// Approvals for this step instance
    /// </summary>
    public virtual ICollection<WorkflowApproval> Approvals { get; set; } = new List<WorkflowApproval>();

}