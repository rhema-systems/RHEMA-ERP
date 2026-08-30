using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Civil's governed inspection envelope over the authoritative Projects quality-checkpoint
/// and non-conformance records. It never becomes a second project-quality or defect owner.
/// </summary>
[Table("ProjectCivilInspectionControls")]
public sealed class ProjectCivilInspectionControl : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid QualityCheckpointId { get; set; }
    public Guid PlanningGisValidationId { get; set; }
    public Guid InspectorUserId { get; set; }
    public Guid? ReinspectionInspectorUserId { get; set; }
    public Guid? NonConformanceId { get; set; }

    [Required, StringLength(500)] public string Purpose { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    [Required, StringLength(512)] public string SpatialReferenceSnapshot { get; set; } = string.Empty;
    [StringLength(4000)] public string? BoundaryCoordinatesSnapshot { get; set; }

    [Required, StringLength(40)] public string Stage { get; set; } = CivilEngineeringInspectionStages.Scheduled;
    [Required, StringLength(40)] public string Status { get; set; } = CivilEngineeringInspectionStatuses.Scheduled;
    [StringLength(4000)] public string? Findings { get; set; }
    [StringLength(2000)] public string? CorrectiveAction { get; set; }
    public DateTime? InspectedAt { get; set; }
    public DateTime? CorrectiveActionRecordedAt { get; set; }
    public DateTime? ReinspectedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }

    public Guid PlanDocumentRecordId { get; set; }
    public Guid PlanDocumentVersionId { get; set; }
    public Guid? InspectionDocumentRecordId { get; set; }
    public Guid? InspectionDocumentVersionId { get; set; }
    public Guid? CorrectiveActionDocumentRecordId { get; set; }
    public Guid? CorrectiveActionDocumentVersionId { get; set; }
    public Guid? ReinspectionDocumentRecordId { get; set; }
    public Guid? ReinspectionDocumentVersionId { get; set; }
    public Guid? ClosureDocumentRecordId { get; set; }
    public Guid? ClosureDocumentVersionId { get; set; }

    public Guid ConfigurationProfileId { get; set; }
    public Guid SupervisionConfigurationDecisionId { get; set; }
    public Guid QualityConfigurationDecisionId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(30)] public string PlanApprovalStatus { get; set; } = CivilEngineeringInspectionPlanApprovalStatuses.Pending;
    public Guid? PlanSubmittedById { get; set; }
    public DateTime? PlanSubmittedAt { get; set; }
    public Guid? PlanApprovedById { get; set; }
    public DateTime? PlanApprovedAt { get; set; }
    [StringLength(2000)] public string? PlanRejectionReason { get; set; }
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
    public ProjectQualityCheckpoint QualityCheckpoint { get; set; } = null!;
    public ProjectCivilPlanningGisValidation PlanningGisValidation { get; set; } = null!;
    public ProjectNonConformance? NonConformance { get; set; }
    public CentralDocumentRecord PlanDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion PlanDocumentVersion { get; set; } = null!;
    public ICollection<ProjectCivilInspectionRevision> Revisions { get; set; } = [];
}

public static class CivilEngineeringInspectionStages
{
    public const string PendingApproval = "PendingApproval";
    public const string Scheduled = "Scheduled";
    public const string CorrectiveActionRequired = "CorrectiveActionRequired";
    public const string ReinspectionScheduled = "ReinspectionScheduled";
    public const string Passed = "Passed";
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";

    public static IReadOnlyList<string> All { get; } = [PendingApproval, Scheduled, CorrectiveActionRequired, ReinspectionScheduled, Passed, Closed, Rejected];
}

public static class CivilEngineeringInspectionStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Scheduled = "Scheduled";
    public const string Active = "Active";
    public const string Blocked = "Blocked";
    public const string Passed = "Passed";
    public const string Closed = "Closed";
    public const string Rejected = "Rejected";

    public static IReadOnlyList<string> All { get; } = [PendingApproval, Scheduled, Active, Blocked, Passed, Closed, Rejected];
}

public static class CivilEngineeringInspectionPlanApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

[Table("ProjectCivilInspectionRevisions")]
public sealed class ProjectCivilInspectionRevision : TenantEntity
{
    public Guid InspectionControlId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(40)] public string FromStage { get; set; } = string.Empty;
    [Required, StringLength(40)] public string ToStage { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;

    public ProjectCivilInspectionControl InspectionControl { get; set; } = null!;
}
