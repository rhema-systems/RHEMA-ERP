using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringDirectTaskLookupOptionDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDirectTaskAssigneeDto
{
    public Guid UserId { get; init; }
    public Guid RoleId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDirectTaskDocumentDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDirectTaskMeasurementUnitDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Symbol { get; init; }
    public string Category { get; init; } = string.Empty;
}

public sealed class CivilEngineeringDirectTaskLookupsDto
{
    public IReadOnlyList<CivilEngineeringDirectTaskAssigneeDto> Assignees { get; init; } = [];
    public IReadOnlyList<CivilEngineeringUrgency> Urgencies { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDirectTaskDocumentDto> Documents { get; init; } = [];
    public bool RequireDueDate { get; init; }
    public int UrgentResponseHours { get; init; }
}

public sealed class CivilEngineeringDirectTaskFeedbackLookupsDto
{
    public IReadOnlyList<CivilEngineeringDirectTaskDocumentDto> Documents { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDirectTaskFeedbackAction> AvailableActions { get; init; } = [];
    public IReadOnlyList<CivilEngineeringDirectTaskMeasurementUnitDto> MeasurementUnits { get; init; } = [];
    public bool RequireFeedbackEvidence { get; init; }
    public bool RequireClosureAcceptance { get; init; }
}

public sealed class CreateCivilEngineeringDirectTaskRequest
{
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Instructions { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public Guid AssignedRoleId { get; set; }
    public CivilEngineeringUrgency Urgency { get; set; }
    [StringLength(1000)] public string? UrgencyReason { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}

public sealed class EscalateCivilEngineeringUrgentTasksRequest
{
    public Guid ClientRequestId { get; set; }
}

public sealed class ProcessCivilEngineeringDirectTaskFeedbackRequest
{
    public Guid ClientRequestId { get; set; }
    public CivilEngineeringDirectTaskFeedbackAction Action { get; set; }
    [StringLength(2000)] public string? Message { get; set; }
    [Range(0, 100)] public decimal? ProgressPercent { get; set; }
    [Range(0, 999999999999d)] public decimal? MeasurementValue { get; set; }
    public Guid? MeasurementUnitId { get; set; }
    public DateTime? CapturedOfflineAtUtc { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Required, StringLength(512)] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CivilEngineeringDirectTaskFeedbackDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public CivilEngineeringDirectTaskFeedbackAction Action { get; init; }
    public decimal? ProgressPercent { get; init; }
    public decimal? MeasurementValue { get; init; }
    public string? MeasurementUnitLabel { get; init; }
    public DateTime? CapturedOfflineAtUtc { get; init; }
    public string? Message { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? DocumentReference { get; init; }
    public string? WorkflowOutcome { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class CivilEngineeringDirectTaskDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid WorkItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
    public Guid AssignedToUserId { get; init; }
    public string AssignedToName { get; init; } = string.Empty;
    public Guid AssignedRoleId { get; init; }
    public string AssignedRoleName { get; init; } = string.Empty;
    public CivilEngineeringUrgency Urgency { get; init; }
    public DateTime DueDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public bool IsUrgentPath { get; init; }
    public string? UrgencyReason { get; init; }
    public DateTime? UrgentResponseDueAt { get; init; }
    public DateTime? UrgentEscalatedAt { get; init; }
    public decimal ProgressPercent { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public string? DocumentReference { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}
