using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringExtensionOfTimePolicy
{
    public static IReadOnlyList<string> ValidateCreate(
        CreateCivilEngineeringExtensionOfTimeRequest request,
        CivilEngineeringExtensionOfTimeValue policy)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (request.ContractId == Guid.Empty) errors.Add("Select the active Works contract for this project.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length < 3) errors.Add("A clear extension-of-time title is required.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3) errors.Add("Provide the extension-of-time reason.");
        if (string.IsNullOrWhiteSpace(request.ScopeSummary) || request.ScopeSummary.Trim().Length < 3) errors.Add("Provide the affected scope summary.");
        if (request.DaysRequested is < 1 or > 3650) errors.Add("Requested extension days must be between 1 and 3650.");
        if (!request.ProposedRevisedCompletionDate.HasValue) errors.Add("Select the proposed revised completion date.");
        if (request.EvidenceDocumentRecordId == Guid.Empty || request.EvidenceDocumentVersionId == Guid.Empty) errors.Add("Select current published central-DMS supporting evidence.");
        if (request.HasCostImpact && policy.RequireQuantitySurveyVariationForCostImpact && (!request.QuantitySurveyVariationOrderId.HasValue || request.QuantitySurveyVariationOrderId == Guid.Empty))
            errors.Add("A current approved and applied QS variation is required when the extension has cost impact.");
        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> ValidateReview(ReviewCivilEngineeringExtensionOfTimeRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("Refresh the extension-of-time request before deciding it.");
        if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Trim().Length < 3) errors.Add("A review comment is required.");
        return errors;
    }
}
