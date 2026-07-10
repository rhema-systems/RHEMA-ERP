using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents a workflow definition that defines the structure and flow of a business process
/// </summary>
[Table("WorkflowDefinitions")]
public class WorkflowDefinition : TenantEntity
{
    /// <summary>
    /// Stable identifier shared by every version in the same workflow family.
    /// </summary>
    public Guid DefinitionKey { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Name of the workflow definition
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the workflow definition
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// ID of the entity type this workflow applies to
    /// </summary>
    [Required]
    public Guid EntityTypeId { get; set; }

    /// <summary>
    /// Version number of the workflow definition
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Draft versions are editable. Published and retired versions are immutable.
    /// </summary>
    public WorkflowDefinitionLifecycleStatus LifecycleStatus { get; set; } = WorkflowDefinitionLifecycleStatus.Draft;

    /// <summary>
    /// Whether this workflow definition is active and can be used
    /// </summary>
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? ChangeSummary { get; set; }

    public Guid? SupersedesDefinitionId { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Guid? PublishedById { get; set; }

    public DateTime? RetiredAt { get; set; }

    public Guid? RetiredById { get; set; }

    /// <summary>
    /// JSON configuration for the workflow (conditions, rules, etc.)
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Configuration { get; set; }


    /// <summary>
    /// Steps that belong to this workflow definition
    /// </summary>
    public virtual ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();

    /// <summary>
    /// Transitions between steps in this workflow definition
    /// </summary>
    public virtual ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();

    /// <summary>
    /// Instances of this workflow definition
    /// </summary>
    public virtual ICollection<WorkflowInstance> Instances { get; set; } = new List<WorkflowInstance>();


    /// <summary>
    /// Navigation property for the entity type this workflow applies to
    /// </summary>
    [ForeignKey(nameof(EntityTypeId))]
    public virtual WorkflowEntityType EntityType { get; set; } = null!;

}
