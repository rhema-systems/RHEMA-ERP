using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents a transition between workflow steps
/// </summary>
[Table("WorkflowTransitions")]
public class WorkflowTransition : TenantEntity
{

    /// <summary>
    /// ID of the workflow definition this transition belongs to
    /// </summary>
    [Required]
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>
    /// ID of the source step
    /// </summary>
    [Required]
    public Guid FromStepId { get; set; }

    /// <summary>
    /// ID of the destination step
    /// </summary>
    [Required]
    public Guid ToStepId { get; set; }

    /// <summary>
    /// Name of the transition
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the transition
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// JSON condition that must be met for this transition to be taken
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Condition { get; set; }

    /// <summary>
    /// Whether this is the default transition if no conditions are met
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Priority of this transition when multiple transitions are possible
    /// </summary>
    public int Priority { get; set; } = 0;


    /// <summary>
    /// Navigation property for the workflow definition this transition belongs to
    /// </summary>
    [ForeignKey(nameof(WorkflowDefinitionId))]
    public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    /// <summary>
    /// Navigation property for the source step
    /// </summary>
    [ForeignKey(nameof(FromStepId))]
    public virtual WorkflowStep FromStep { get; set; } = null!;

    /// <summary>
    /// Navigation property for the destination step
    /// </summary>
    [ForeignKey(nameof(ToStepId))]
    public virtual WorkflowStep ToStep { get; set; } = null!;

}