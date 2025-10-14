using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Workflow;

/// <summary>
/// Represents a configurable entity type that workflows can be applied to
/// </summary>
[Table("WorkflowEntityTypes")]
public class WorkflowEntityType : TenantEntity
{

    /// <summary>
    /// Code/key for the entity type (e.g., "WORK_ORDER", "PURCHASE_REQUEST")
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the entity type
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the entity type (alias for Name for compatibility)
    /// </summary>
    public string DisplayName => Name;

    /// <summary>
    /// Description of what this entity type represents
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// .NET type name for the entity (e.g., "ErpSystem.Core.Entities.Maintenance.WorkOrder")
    /// </summary>
    [StringLength(200)]
    public string? EntityClassName { get; set; }

    /// <summary>
    /// JSON schema defining the properties available for workflow conditions
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? PropertySchema { get; set; }

    /// <summary>
    /// Whether this entity type is active and can be used in workflows
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order for the entity type in lists
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// Icon or CSS class for displaying this entity type
    /// </summary>
    [StringLength(50)]
    public string? Icon { get; set; }

    /// <summary>
    /// Color code for this entity type (for UI theming)
    /// </summary>
    [StringLength(7)]
    public string? ColorCode { get; set; }


    /// <summary>
    /// Workflow definitions that use this entity type
    /// </summary>
    public virtual ICollection<WorkflowDefinition> WorkflowDefinitions { get; set; } = new List<WorkflowDefinition>();

}