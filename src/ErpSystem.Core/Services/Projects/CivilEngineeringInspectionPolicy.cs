using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringInspectionPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringInspectionControlRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (request.PlanningGisValidationId == Guid.Empty) errors.Add("Select the approved Planning/GIS site validation.");
        if (request.InspectorUserId == Guid.Empty) errors.Add("Select a qualified project inspector.");
        if (request.ScheduledAt == default) errors.Add("A scheduled inspection date is required.");
        if (string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Trim().Length < 3) errors.Add("Inspection purpose must contain at least three characters.");
        if (request.PlanDocumentRecordId == Guid.Empty || request.PlanDocumentVersionId == Guid.Empty) errors.Add("Select current published central-DMS inspection-plan evidence.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateProcess(ProcessCivilEngineeringInspectionControlRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("The latest inspection version is required. Refresh and retry.");
        if (!Enum.IsDefined(request.Action)) errors.Add("Select a valid inspection action.");
        if (request.Action == CivilEngineeringInspectionAction.RejectPlan
            && (string.IsNullOrWhiteSpace(request.ReviewComment) || request.ReviewComment.Trim().Length < 3))
            errors.Add("A plan-rejection reason containing at least three characters is required.");
        if (request.Action is CivilEngineeringInspectionAction.RecordPassed or CivilEngineeringInspectionAction.RecordFailed or CivilEngineeringInspectionAction.RecordReinspectionPassed or CivilEngineeringInspectionAction.RecordReinspectionFailed)
        {
            if (string.IsNullOrWhiteSpace(request.Findings) || request.Findings.Trim().Length < 3) errors.Add("Inspection findings must contain at least three characters.");
            if (!request.CentralDocumentRecordId.HasValue || !request.CentralDocumentVersionId.HasValue) errors.Add("Select current published central-DMS inspection evidence.");
        }
        if (request.Action == CivilEngineeringInspectionAction.RecordCorrectiveAction)
        {
            if (string.IsNullOrWhiteSpace(request.CorrectiveAction) || request.CorrectiveAction.Trim().Length < 3) errors.Add("Corrective action must contain at least three characters.");
            if (!request.ReinspectionInspectorUserId.HasValue || request.ReinspectionInspectorUserId == Guid.Empty) errors.Add("Select an independent qualified reinspection inspector.");
            if (!request.CentralDocumentRecordId.HasValue || !request.CentralDocumentVersionId.HasValue) errors.Add("Select current published central-DMS corrective-action evidence.");
        }
        if (request.Action == CivilEngineeringInspectionAction.Close && (!request.CentralDocumentRecordId.HasValue || !request.CentralDocumentVersionId.HasValue)) errors.Add("Select current published central-DMS closure evidence.");
        return errors;
    }

    public static bool IsTerminal(string stage) =>
        string.Equals(stage, CivilEngineeringInspectionStages.Closed, StringComparison.Ordinal)
        || string.Equals(stage, CivilEngineeringInspectionStages.Rejected, StringComparison.Ordinal);
}
