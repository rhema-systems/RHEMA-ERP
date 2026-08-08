using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementControlEvents")]
public sealed class ProcurementControlEvent : TenantEntity
{
    [Required, StringLength(200)] public string EventKey { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    [Required, StringLength(100)] public string EventType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public AuditOperationKind Operation { get; set; }
    public ProcurementControlEventResult Result { get; set; }
    [StringLength(200)] public string? RuleCode { get; set; }
    public Guid? RuleId { get; set; }
    [StringLength(100)] public string? RuleVersion { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string DecisionKeysJson { get; set; } = "[]";
    [Required, StringLength(100)] public string SourceType { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    [Required, StringLength(500)] public string SourceReference { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string ActorRolesJson { get; set; } = "[]";
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? InputValuesJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? ResultValuesJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(100)] public string? CausationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ApplicationUser ActorUser { get; set; } = null!;
    public ICollection<ProcurementControlEventEvidenceLink> EvidenceLinks { get; set; } = new List<ProcurementControlEventEvidenceLink>();
}

[Table("ProcurementControlEventEvidenceLinks")]
public sealed class ProcurementControlEventEvidenceLink : TenantEntity
{
    public Guid ControlEventId { get; set; }
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(200)] public string Reference { get; set; } = string.Empty;
    [StringLength(300)] public string? Label { get; set; }
    [StringLength(200)] public string? RequirementKey { get; set; }

    public ProcurementControlEvent ControlEvent { get; set; } = null!;
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}
