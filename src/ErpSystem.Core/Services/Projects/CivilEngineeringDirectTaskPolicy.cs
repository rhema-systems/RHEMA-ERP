using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringDirectTaskPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringDirectTaskRequest request, CivilEngineeringTaskAssignmentValue policy, DateTime utcNow)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length is < 3 or > 200) errors.Add("Task title must contain 3 to 200 characters.");
        if (string.IsNullOrWhiteSpace(request.Instructions) || request.Instructions.Trim().Length is < 5 or > 4000) errors.Add("Task instructions must contain 5 to 4,000 characters.");
        if (request.AssignedToUserId == Guid.Empty || request.AssignedRoleId == Guid.Empty) errors.Add("Select an eligible project assignee and role.");
        if (!policy.AllowedUrgencies.Contains(request.Urgency)) errors.Add("The selected urgency is not enabled by the effective CIV-CFG-010 policy.");
        if (policy.RequireDueDate && !request.DueDate.HasValue) errors.Add("The effective CIV-CFG-010 policy requires a due date.");
        if (request.DueDate.HasValue && request.DueDate.Value.ToUniversalTime() <= utcNow) errors.Add("The task due date must be in the future.");
        if (IsUrgent(request.Urgency))
        {
            if (string.IsNullOrWhiteSpace(request.UrgencyReason) || request.UrgencyReason.Trim().Length is < 5 or > 1000)
                errors.Add("Urgent or emergency assignments require a reason of 5 to 1,000 characters.");
            if (!request.DueDate.HasValue) errors.Add("Urgent or emergency assignments require an SLA response deadline.");
            if (request.DueDate.HasValue && request.DueDate.Value.ToUniversalTime() > utcNow.AddHours(policy.UrgentResponseHours))
                errors.Add("The urgent response deadline cannot exceed the CIV-CFG-010 urgent response SLA.");
            if (policy.UrgentEscalationRoleIds.Count == 0)
                errors.Add("CIV-CFG-010 must select one or more urgent escalation roles before urgent work can be assigned.");
        }
        if (request.CentralDocumentRecordId.HasValue != request.CentralDocumentVersionId.HasValue) errors.Add("Select both the central-DMS document and its current Published version, or leave both blank.");
        return errors;
    }

    public static string WorkItemPriority(CivilEngineeringUrgency urgency) => urgency switch
    {
        CivilEngineeringUrgency.Emergency => "Critical",
        CivilEngineeringUrgency.Urgent => "High",
        CivilEngineeringUrgency.Priority => "High",
        _ => "Normal"
    };

    public static bool IsUrgent(CivilEngineeringUrgency urgency)
        => urgency is CivilEngineeringUrgency.Urgent or CivilEngineeringUrgency.Emergency;

    public static IReadOnlyList<string> ValidateFeedback(ProcessCivilEngineeringDirectTaskFeedbackRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("The task row version is required. Refresh the task and retry.");
        if (!Enum.IsDefined(request.Action)) errors.Add("Select a valid Civil task feedback action.");
        if (request.Message?.Trim().Length > 2000) errors.Add("Feedback text cannot exceed 2,000 characters.");
        if (request.MeasurementValue.HasValue != request.MeasurementUnitId.HasValue)
            errors.Add("Select both a measurement value and an active unit of measure, or leave both blank.");
        if (request.MeasurementValue is < 0) errors.Add("Measurement value cannot be negative.");
        if (request.CapturedOfflineAtUtc.HasValue)
        {
            var capturedAt = request.CapturedOfflineAtUtc.Value.ToUniversalTime();
            if (capturedAt > DateTime.UtcNow.AddMinutes(5)) errors.Add("An offline capture time cannot be in the future.");
            if (capturedAt < DateTime.UtcNow.AddDays(-31)) errors.Add("An offline capture time cannot be older than 31 days.");
        }
        if (request.CentralDocumentRecordId.HasValue != request.CentralDocumentVersionId.HasValue)
            errors.Add("Select both the central-DMS document and its current Published version, or leave both blank.");
        return errors;
    }
}
