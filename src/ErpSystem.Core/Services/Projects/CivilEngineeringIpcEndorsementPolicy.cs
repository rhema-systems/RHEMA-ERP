using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

/// <summary>
/// Stateless IPC review rules. The QS payment-certificate lifecycle calls the guard before
/// its own workflow submission; it never duplicates QS or Finance state transitions.
/// </summary>
public static class CivilEngineeringIpcEndorsementPolicy
{
    public static IReadOnlyList<string> ValidateSubmission(SubmitCivilEngineeringIpcEndorsementRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (!HasReason(request.Notes)) errors.Add("Provide IPC handoff notes of at least 5 characters.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateReview(
        ReviewCivilEngineeringIpcEndorsementRequest request,
        bool requireEvidence)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("The IPC review row version is required.");
        if (!HasReason(request.Notes)) errors.Add("Provide a Project Engineer decision note of at least 5 characters.");
        if (!request.Endorse && (request.EvidenceDocumentRecordId.HasValue || request.EvidenceDocumentVersionId.HasValue))
            errors.Add("Return to Projects Coordinator must not attach endorsement evidence.");
        if (request.Endorse && requireEvidence && (!request.EvidenceDocumentRecordId.HasValue || !request.EvidenceDocumentVersionId.HasValue))
            errors.Add("Select the current Published DMS endorsement evidence before endorsing the IPC.");
        if ((request.EvidenceDocumentRecordId.HasValue && !request.EvidenceDocumentVersionId.HasValue)
            || (!request.EvidenceDocumentRecordId.HasValue && request.EvidenceDocumentVersionId.HasValue))
            errors.Add("Select both the DMS document and its current Published version.");
        return errors;
    }

    public static void RequireCanAdvance(ProjectCivilIpcEndorsement? latestReview, bool hasCurrentProjectEngineer)
    {
        if (latestReview?.Status == CivilEngineeringIpcEndorsementStatuses.Endorsed) return;
        if (latestReview is not null)
            throw new InvalidOperationException(latestReview.Status == CivilEngineeringIpcEndorsementStatuses.ReturnedToProjectsCoordinator
                ? "The IPC was returned to Projects Coordinator. Amend it if required and submit a new Project Engineer review."
                : "The IPC is awaiting the assigned Project Engineer's endorsement.");
        if (hasCurrentProjectEngineer)
            throw new InvalidOperationException("This Civil project has an active Project Engineer. Submit the IPC for engineering review before onward QS processing.");
    }

    public static bool IsAmendmentBlocked(ProjectCivilIpcEndorsement? latestReview) => latestReview?.Status is
        CivilEngineeringIpcEndorsementStatuses.AwaitingProjectEngineerReview or CivilEngineeringIpcEndorsementStatuses.Endorsed;

    private static bool HasReason(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length is >= 5 and <= 2000;
}
