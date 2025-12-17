using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents an instance of a workflow definition being executed for a specific entity
/// </summary>
[Table("WorkflowInstances")]
public class WorkflowInstance : TenantEntity
{

    /// <summary>
    /// ID of the workflow definition this instance is based on
    /// </summary>
    [Required]
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>
    /// ID of the entity this workflow is being executed for
    /// </summary>
    [Required]
    public Guid EntityId { get; set; }

    /// <summary>
    /// ID of the entity type this workflow is being executed for
    /// </summary>
    [Required]
    public Guid EntityTypeId { get; set; }

    /// <summary>
    /// Current status of the workflow instance
    /// </summary>
    [Required]
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Created;

    /// <summary>
    /// Priority of this workflow instance
    /// </summary>
    public WorkflowPriority Priority { get; set; } = WorkflowPriority.Normal;

    /// <summary>
    /// ID of the user who initiated this workflow
    /// </summary>
    [Required]
    public Guid InitiatedById { get; set; }

    /// <summary>
    /// ID of the user who started this workflow (may be different from initiator)
    /// </summary>
    public Guid? StartedById { get; set; }

    /// <summary>
    /// Date when the workflow instance was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the workflow instance was started
    /// </summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>
    /// Date when the workflow instance was completed
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Date when the workflow instance was cancelled
    /// </summary>
    public DateTime? CancelledDate { get; set; }

    /// <summary>
    /// JSON data context for the workflow execution
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? DataContext { get; set; }

    /// <summary>
    /// Additional data for the workflow execution (alias for compatibility)
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Data { get; set; }

    /// <summary>
    /// Notes or comments for this workflow instance
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Notes { get; set; }

    /// <summary>
    /// ID of the current step being executed (if any)
    /// </summary>
    public Guid? CurrentStepId { get; set; }


    /// <summary>
    /// Navigation property for the workflow definition this instance is based on
    /// </summary>
    [ForeignKey(nameof(WorkflowDefinitionId))]
    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    /// <summary>
    /// Navigation property for the user who initiated this workflow
    /// </summary>
    [ForeignKey(nameof(InitiatedById))]
    public virtual ApplicationUser InitiatedBy { get; set; } = null!;

    /// <summary>
    /// Navigation property for the current step
    /// </summary>
    [ForeignKey(nameof(CurrentStepId))]
    public virtual WorkflowStep? CurrentStep { get; set; }

    /// <summary>
    /// Navigation property for the entity type this workflow is being executed for
    /// </summary>
    [ForeignKey(nameof(EntityTypeId))]
    public virtual WorkflowEntityType EntityType { get; set; } = null!;

    /// <summary>
    /// Step instances for this workflow instance
    /// </summary>
    public virtual ICollection<WorkflowStepInstance> StepInstances { get; set; } = new List<WorkflowStepInstance>();

    /// <summary>
    /// Activity log entries for this workflow instance
    /// </summary>
    public virtual ICollection<WorkflowActivityLog> ActivityLogs { get; set; } = new List<WorkflowActivityLog>();

}
