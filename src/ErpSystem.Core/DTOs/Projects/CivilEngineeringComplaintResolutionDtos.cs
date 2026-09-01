namespace ErpSystem.Core.DTOs.Projects;

/// <summary>
/// Read-only Civil projection of a Helpdesk company-building complaint. Helpdesk remains the
/// ticket and requester-closure owner; Civil, Maintenance, DMS and Finance remain owners of
/// their respective linked records.
/// </summary>
public sealed class CivilEngineeringComplaintResolutionDto
{
    public Guid HelpdeskTicketId { get; init; }
    public string ComplaintTicketNumber { get; init; } = string.Empty;
    public string? ComplaintSubject { get; init; }
    public string HelpdeskStatus { get; init; } = string.Empty;
    public DateTime ComplaintLoggedAt { get; init; }
    public Guid IntakeId { get; init; }
    public string IntakeNumber { get; init; } = string.Empty;
    public string IntakeStatus { get; init; } = string.Empty;
    public Guid? AssessmentId { get; init; }
    public string? AssessmentStage { get; init; }
    public Guid? CostingHandoffId { get; init; }
    public string? CostingStage { get; init; }
    public Guid? ExecutionLinkId { get; init; }
    public string? ExecutionStage { get; init; }
    public string? WorkOrderNumber { get; init; }
    public string? WorkOrderStatus { get; init; }
    public Guid? CompletionControlId { get; init; }
    public string? CompletionStage { get; init; }
    public string? InspectionStatus { get; init; }
    public string? PaymentDirectionStatus { get; init; }
    public string ResolutionStage { get; init; } = string.Empty;
    public string ResolutionLabel { get; init; } = string.Empty;
    public bool ReadyForHelpdeskResolution { get; init; }
    public DateTime LastActivityAt { get; init; }
}

public sealed class CivilEngineeringComplaintResolutionTimelineEntryDto
{
    public DateTime OccurredAt { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Stage { get; init; }
    public string? ActorName { get; init; }
}
