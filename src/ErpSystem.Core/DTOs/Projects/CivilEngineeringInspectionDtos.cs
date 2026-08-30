using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringInspectionLookupOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringInspectionPlanningLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string SpatialReference { get; init; } = string.Empty;
}

public sealed class CivilEngineeringInspectionDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringInspectionLookupsDto
{
    public IReadOnlyList<CivilEngineeringInspectionPlanningLookupDto> PlanningGisValidations { get; init; } = [];
    public IReadOnlyList<CivilEngineeringInspectionLookupOptionDto> Inspectors { get; init; } = [];
    public IReadOnlyList<CivilEngineeringInspectionDocumentLookupDto> Documents { get; init; } = [];
    public bool RequireIndependentReinspection { get; init; }
    public bool BlockCompletionOnFailure { get; init; }
}

public sealed class CivilEngineeringInspectionControlDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid QualityCheckpointId { get; init; }
    public Guid PlanningGisValidationId { get; init; }
    public string PlanningGisValidationLabel { get; init; } = string.Empty;
    public Guid InspectorUserId { get; init; }
    public string InspectorName { get; init; } = string.Empty;
    public Guid? ReinspectionInspectorUserId { get; init; }
    public string? ReinspectionInspectorName { get; init; }
    public Guid? NonConformanceId { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
    public string SpatialReferenceSnapshot { get; init; } = string.Empty;
    public string? BoundaryCoordinatesSnapshot { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid WorkflowDefinitionId { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string PlanApprovalStatus { get; init; } = string.Empty;
    public Guid? PlanApprovedById { get; init; }
    public DateTime? PlanApprovedAt { get; init; }
    public string? PlanRejectionReason { get; init; }
    public string? Findings { get; init; }
    public string? CorrectiveAction { get; init; }
    public DateTime? InspectedAt { get; init; }
    public DateTime? CorrectiveActionRecordedAt { get; init; }
    public DateTime? ReinspectedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public Guid PlanDocumentRecordId { get; init; }
    public Guid PlanDocumentVersionId { get; init; }
    public Guid? InspectionDocumentRecordId { get; init; }
    public Guid? InspectionDocumentVersionId { get; init; }
    public Guid? CorrectiveActionDocumentRecordId { get; init; }
    public Guid? CorrectiveActionDocumentVersionId { get; init; }
    public Guid? ReinspectionDocumentRecordId { get; init; }
    public Guid? ReinspectionDocumentVersionId { get; init; }
    public Guid? ClosureDocumentRecordId { get; init; }
    public Guid? ClosureDocumentVersionId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CivilEngineeringInspectionRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string FromStage { get; init; } = string.Empty;
    public string ToStage { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CreateCivilEngineeringInspectionControlRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid PlanningGisValidationId { get; set; }
    public Guid InspectorUserId { get; set; }
    public DateTime ScheduledAt { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string Purpose { get; set; } = string.Empty;
    public Guid PlanDocumentRecordId { get; set; }
    public Guid PlanDocumentVersionId { get; set; }
}

public enum CivilEngineeringInspectionAction
{
    RecordPassed = 1,
    RecordFailed = 2,
    RecordCorrectiveAction = 3,
    RecordReinspectionPassed = 4,
    RecordReinspectionFailed = 5,
    Close = 6,
    ApprovePlan = 7,
    RejectPlan = 8
}

public sealed class ProcessCivilEngineeringInspectionControlRequest
{
    public Guid ClientRequestId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public CivilEngineeringInspectionAction Action { get; set; }
    [StringLength(4000)] public string? Findings { get; set; }
    [StringLength(2000)] public string? CorrectiveAction { get; set; }
    [StringLength(2000)] public string? ReviewComment { get; set; }
    public Guid? ReinspectionInspectorUserId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}
