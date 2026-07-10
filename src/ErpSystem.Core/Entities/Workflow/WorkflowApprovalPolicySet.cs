using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

[Table("WorkflowApprovalPolicySets")]
public class WorkflowApprovalPolicySet : TenantEntity
{
    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Module { get; set; }

    [StringLength(100)]
    public string? EntityType { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public Guid? LocationId { get; set; }
    public Guid? LegalEntityId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinimumAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaximumAmount { get; set; }

    [StringLength(3)]
    public string? CurrencyCode { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public WorkflowDefinitionLifecycleStatus LifecycleStatus { get; set; } = WorkflowDefinitionLifecycleStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? RetiredAt { get; set; }
    public Guid? RetiredById { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")]
    public string ApprovalConfiguration { get; set; } = "{}";
}
