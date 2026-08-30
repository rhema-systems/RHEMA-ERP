using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Projects;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Civil Engineering's governed control envelope over the authoritative Projects EOT record.
/// It deliberately stores no variation value, budget, contract amendment or Finance posting.
/// Those remain owned by Quantity Survey, Finance and Procurement respectively.
/// </summary>
[Table("ProjectCivilExtensionOfTimeControls")]
public sealed class ProjectCivilExtensionOfTimeControl : TenantEntity, ICivilEngineeringWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid ProjectExtensionOfTimeId { get; set; }
    public Guid ContractId { get; set; }
    public Guid? QuantitySurveyVariationOrderId { get; set; }

    [Required, StringLength(500)] public string ScopeSummary { get; set; } = string.Empty;
    public bool HasCostImpact { get; set; }

    [Required, StringLength(40)] public string Status { get; set; } = "PendingApproval";
    [Required, StringLength(40)] public string ApprovalStatus { get; set; } = "Pending";
    public Guid SubmittedById { get; set; }
    public DateTime SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    public Guid EvidenceDocumentRecordId { get; set; }
    public Guid EvidenceDocumentVersionId { get; set; }

    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [Required, StringLength(80)] public string EvidenceMetadataTemplateCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;

    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectExtensionOfTime ExtensionOfTime { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public ProjectVariationOrder? QuantitySurveyVariationOrder { get; set; }
    public CentralDocumentRecord EvidenceDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion EvidenceDocumentVersion { get; set; } = null!;
    public CivilEngineeringConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public CivilEngineeringConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<ProjectCivilExtensionOfTimeRevision> Revisions { get; set; } = [];
}

[Table("ProjectCivilExtensionOfTimeRevisions")]
public sealed class ProjectCivilExtensionOfTimeRevision : TenantEntity
{
    public Guid ExtensionOfTimeControlId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(40)] public string FromStatus { get; set; } = string.Empty;
    [Required, StringLength(40)] public string ToStatus { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;

    public ProjectCivilExtensionOfTimeControl ExtensionOfTimeControl { get; set; } = null!;
}
