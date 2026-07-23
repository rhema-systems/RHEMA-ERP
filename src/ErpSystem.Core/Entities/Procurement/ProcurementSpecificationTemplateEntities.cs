using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSpecificationTemplates")]
public sealed class ProcurementSpecificationTemplate : TenantEntity
{
    public Guid TemplateKey { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string TemplateCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public ProcurementSpecificationTemplateKind Kind { get; set; }
    public int Version { get; set; } = 1;
    public ProcurementSpecificationTemplateStatus Status { get; set; } = ProcurementSpecificationTemplateStatus.Draft;
    public bool IsDefault { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }

    [StringLength(1000)]
    public string? ChangeSummary { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string Purpose { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string FunctionalAndPerformanceRequirements { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string ProcessAndMaterialsRequirements { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string DimensionsAndMarkingRequirements { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string TestingAndInspectionRequirements { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string ApplicableStandards { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string Deliverables { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string AcceptanceCriteria { get; set; } = string.Empty;

    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesTemplateId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedById { get; set; }

    [StringLength(300)]
    public string? SubmittedByName { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }

    [StringLength(300)]
    public string? PublishedByName { get; set; }

    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredById { get; set; }

    [StringLength(300)]
    public string? RetiredByName { get; set; }

    [StringLength(1000)]
    public string? ReviewComment { get; set; }

    public int RevisionNumber { get; set; } = 1;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ProcurementSpecificationTemplate? SupersedesTemplate { get; set; }
}
