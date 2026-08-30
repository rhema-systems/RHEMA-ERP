using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringDevelopmentApprovalFilePolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringDevelopmentApprovalFileRequest request, DateTime now)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (request.ProjectId == Guid.Empty) errors.Add("Select the controlled project.");
        if (request.EstateManagedAssetId == Guid.Empty) errors.Add("Select the controlled property or building.");
        if (string.IsNullOrWhiteSpace(request.ApplicationReference) || request.ApplicationReference.Trim().Length < 3) errors.Add("An application reference is required.");
        if (!request.ApplicantBusinessPartnerId.HasValue && string.IsNullOrWhiteSpace(request.ApplicantName)) errors.Add("Select a registered applicant or enter the external applicant name.");
        if (request.DueDate == default || request.DueDate.Date < now.Date) errors.Add("Select a due date that is today or later.");
        if (request.SiteInspectionDueDate.HasValue && request.SiteInspectionDueDate.Value.Date > request.DueDate.Date) errors.Add("The site-inspection due date cannot be later than the file due date.");
        if (request.ApplicationEvidence.Count == 0 || request.ApplicationEvidence.Any(item => item.CentralDocumentRecordId == Guid.Empty || item.CentralDocumentVersionId == Guid.Empty)) errors.Add("Attach at least one current Published central-DMS application evidence document.");
        if (request.ApplicationEvidence.Select(item => item.CentralDocumentVersionId).Distinct().Count() != request.ApplicationEvidence.Count) errors.Add("Each application document version may be attached once.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateInspection(RecordCivilEngineeringSiteInspectionRequest request, DateTime now)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (request.SiteInspectedAt == default || request.SiteInspectedAt > now) errors.Add("Enter a site inspection timestamp that is not in the future.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("A row version is required. Refresh the file and retry.");
        if (request.Evidence.Count == 0 || request.Evidence.Any(item => item.CentralDocumentRecordId == Guid.Empty || item.CentralDocumentVersionId == Guid.Empty)) errors.Add("Attach at least one current Published central-DMS site-inspection document.");
        if (request.Evidence.Select(item => item.CentralDocumentVersionId).Distinct().Count() != request.Evidence.Count) errors.Add("Each site-inspection document version may be attached once.");
        return errors;
    }
}
