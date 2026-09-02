using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

/// <summary>Pure lifecycle guard for Civil's work-level completion envelope.</summary>
public static class CivilEngineeringMaintenanceCompletionControlPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringMaintenanceCompletionControlRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (request.ExecutionLinkId == Guid.Empty) errors.Add("Select a completed Civil Maintenance execution link.");
        if (string.IsNullOrWhiteSpace(request.CompletionSummary) || request.CompletionSummary.Trim().Length < 3) errors.Add("Provide a completion summary.");
        if (request.CompletionDocumentRecordId == Guid.Empty || request.CompletionDocumentVersionId == Guid.Empty) errors.Add("Select the current published central-DMS completion report and version.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateProcess(ProcessCivilEngineeringMaintenanceCompletionControlRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("Refresh the completion control before processing it.");
        if (!Enum.IsDefined(request.Action)) errors.Add("Select a valid completion-control action.");
        if (RequiresNote(request.Action) && (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length < 3)) errors.Add("A decision, direction or outcome note is required.");
        if (RequiresEvidence(request.Action) && (request.CentralDocumentRecordId is not { } recordId || recordId == Guid.Empty || request.CentralDocumentVersionId is not { } versionId || versionId == Guid.Empty)) errors.Add("Select both the central-DMS evidence document and its current version.");
        if (request.CentralDocumentRecordId.HasValue != request.CentralDocumentVersionId.HasValue) errors.Add("Select both the central-DMS evidence document and version, or neither.");
        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    public static bool RequiresEvidence(CivilEngineeringMaintenanceCompletionAction action) => action is
        CivilEngineeringMaintenanceCompletionAction.DirectInspection or
        CivilEngineeringMaintenanceCompletionAction.RecordInspectionPassed or
        CivilEngineeringMaintenanceCompletionAction.RecordInspectionFailed or
        CivilEngineeringMaintenanceCompletionAction.DirectPayment;

    public static bool RequiresNote(CivilEngineeringMaintenanceCompletionAction action) => action is not CivilEngineeringMaintenanceCompletionAction.SubmitToHod;

    public static bool IsTerminal(string stage) => string.Equals(stage, CivilEngineeringMaintenanceCompletionStages.Closed, StringComparison.Ordinal);
}
