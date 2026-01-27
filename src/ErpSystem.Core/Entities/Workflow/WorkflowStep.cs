using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents a step in a workflow definition
/// </summary>
[Table("WorkflowSteps")]
public class WorkflowStep : TenantEntity
{

    /// <summary>
    /// ID of the workflow definition this step belongs to
    /// </summary>
    [Required]
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>
    /// Name of the workflow step
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the workflow step
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Type of the workflow step (Manual, Automatic, Approval, etc.)
    /// </summary>
    [Required]
    public WorkflowStepType StepType { get; set; }

    /// <summary>
    /// Order/sequence of this step in the workflow
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Display order for this step (alias for Order)
    /// </summary>
    public int StepOrder => Order;

    /// <summary>
    /// Whether this is the starting step of the workflow
    /// </summary>
    public bool IsStartStep { get; set; } = false;

    /// <summary>
    /// Whether this is an ending step of the workflow
    /// </summary>
    public bool IsEndStep { get; set; } = false;

    /// <summary>
    /// Type of assignment for this step
    /// </summary>
    [StringLength(50)]
    public string? AssignmentType { get; set; }

    /// <summary>
    /// JSON configuration for assignment (users, roles, groups, etc.)
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? AssignmentConfiguration { get; set; }

    /// <summary>
    /// Whether this step is required and cannot be skipped
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Required role to execute this step (optional)
    /// </summary>
    [StringLength(100)]
    public string? RequiredRole { get; set; }

    /// <summary>
    /// Estimated time in hours to complete this step
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public double? EstimatedHours { get; set; }

    /// <summary>
    /// JSON configuration for the step (forms, approvers, conditions, etc.)
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Configuration { get; set; }


    /// <summary>
    /// Navigation property for the workflow definition this step belongs to
    /// </summary>
    [ForeignKey(nameof(WorkflowDefinitionId))]
    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    /// <summary>
    /// Transitions that start from this step
    /// </summary>
    public virtual ICollection<WorkflowTransition> OutgoingTransitions { get; set; } = new List<WorkflowTransition>();

    /// <summary>
    /// Transitions that end at this step
    /// </summary>
    public virtual ICollection<WorkflowTransition> IncomingTransitions { get; set; } = new List<WorkflowTransition>();

    /// <summary>
    /// Instances of this step
    /// </summary>
    public virtual ICollection<WorkflowStepInstance> StepInstances { get; set; } = new List<WorkflowStepInstance>();

}
