using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

/// <summary>Pure validation and lifecycle guards for the governed Civil RFI envelope.</summary>
public static class CivilEngineeringRfiRoutingPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringRfiRequest request, bool requiresEvidence)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.ReferenceNumber)) errors.Add("RFI reference number is required.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length < 3) errors.Add("RFI subject must contain at least 3 characters.");
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Trim().Length < 10) errors.Add("RFI question must contain at least 10 characters.");
        if (request.Priority is not (ProjectRfiPriorities.Low or ProjectRfiPriorities.Medium or ProjectRfiPriorities.High or ProjectRfiPriorities.Critical)) errors.Add("Select a valid RFI priority.");
        if (requiresEvidence && (request.Evidence?.Count ?? 0) == 0) errors.Add("Select current published central-DMS evidence before submitting the RFI.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateProjectEngineerResponse(SubmitCivilEngineeringRfiResponseRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("Refresh the RFI before submitting a response.");
        if (string.IsNullOrWhiteSpace(request.ResponseText) || request.ResponseText.Trim().Length < 10) errors.Add("The Project Engineer response must contain at least 10 characters.");
        if (request.CentralDocumentRecordId == Guid.Empty || request.CentralDocumentVersionId == Guid.Empty) errors.Add("Select a current published central-DMS response document and version.");
        return errors;
    }

    public static bool CanProjectEngineerRespond(string status)
        => status is CivilEngineeringRfiRoutingStatuses.AwaitingProjectEngineerResponse
           or CivilEngineeringRfiRoutingStatuses.ReturnedToProjectEngineer;

    public static bool CanProjectManagerDecide(string status, string approvalStatus)
        => status == CivilEngineeringRfiRoutingStatuses.AwaitingProjectManagerApproval
           && string.Equals(approvalStatus, "Pending", StringComparison.OrdinalIgnoreCase);
}
