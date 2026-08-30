using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringQualityTestPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringQualityTestReportRequest request, CivilEngineeringQualityTestValue policy, DateTime now)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.ReportReference) || request.ReportReference.Trim().Length < 3) errors.Add("Test report reference must contain at least 3 characters.");
        if (!policy.TestCategories.Contains(request.TestCategory)) errors.Add("Select a test category permitted by the effective Civil test policy.");
        if (!CivilEngineeringQualityTestSourceTypes.All.Contains(request.SourceType, StringComparer.Ordinal)) errors.Add("Select a controlled test source type.");
        if ((request.SourceType is CivilEngineeringQualityTestSourceTypes.Laboratory or CivilEngineeringQualityTestSourceTypes.Contractor or CivilEngineeringQualityTestSourceTypes.Consultant) && !request.SourceBusinessPartnerId.HasValue) errors.Add("Select the controlled laboratory, contractor, or consultant source.");
        if (request.SourceType == CivilEngineeringQualityTestSourceTypes.Internal && request.SourceBusinessPartnerId.HasValue) errors.Add("An internal test source cannot be linked to an external business partner.");
        if (request.TestedAt == default || request.TestedAt.ToUniversalTime() > now.AddMinutes(5)) errors.Add("Tested date cannot be in the future.");
        if (!CivilEngineeringQualityTestResultStatuses.All.Contains(request.ResultStatus, StringComparer.Ordinal)) errors.Add("Select Pass, Fail, or Inconclusive as the controlled result.");
        if (string.IsNullOrWhiteSpace(request.ResultSummary) || request.ResultSummary.Trim().Length < 3) errors.Add("Provide the test result summary.");
        if (request.ReviewerUserId == Guid.Empty) errors.Add("Select a controlled independent reviewer.");
        if (request.CentralDocumentRecordId == Guid.Empty || request.CentralDocumentVersionId == Guid.Empty) errors.Add("Select the current Published central-DMS test evidence.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateDecision(ProcessCivilEngineeringQualityTestReportRequest request, bool requiresEndorsementEvidence)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("Refresh and supply the current test-report concurrency token.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3) errors.Add("Provide a concise endorsement or rejection reason.");
        if (request.Approve && requiresEndorsementEvidence && (!request.EndorsementDocumentRecordId.HasValue || !request.EndorsementDocumentVersionId.HasValue)) errors.Add("Select the current Published central-DMS endorsement evidence.");
        if (!request.Approve && (request.EndorsementDocumentRecordId.HasValue != request.EndorsementDocumentVersionId.HasValue)) errors.Add("Select both the endorsement record and version, or neither.");
        return errors;
    }
}
