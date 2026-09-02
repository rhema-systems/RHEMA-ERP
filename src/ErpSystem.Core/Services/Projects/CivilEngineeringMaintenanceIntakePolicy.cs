using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringMaintenanceIntakePolicy
{
    public static IReadOnlyList<string> ValidateCreate(
        CreateCivilEngineeringMaintenanceIntakeRequest request,
        CivilEngineeringMaintenanceAssessmentValue policy)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length < 3) errors.Add("A concise intake title is required.");
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 3) errors.Add("Describe the maintenance issue or complaint.");
        if (!policy.AllowedRequestSources.Contains(request.Source)) errors.Add("The selected source is not enabled by CIV-CFG-007.");
        if (!policy.AllowedUrgencies.Contains(request.Urgency)) errors.Add("The selected urgency is not enabled by CIV-CFG-007.");
        if (request.RequesterUserId == Guid.Empty) errors.Add("Select the controlled requester.");
        if (request.PriorityLevelId == Guid.Empty) errors.Add("Select the controlled maintenance priority.");
        if (request.CentralDocumentRecordId == Guid.Empty || request.CentralDocumentVersionId == Guid.Empty) errors.Add("Select a current Published central-DMS intake evidence document.");

        var targetCount = (request.MaintenanceAssetId.HasValue ? 1 : 0) + (request.EstateManagedAssetId.HasValue ? 1 : 0);
        // CIV-0301 always captures the governed physical target. The configuration
        // flag remains frozen for later assessment behaviour, but cannot weaken the
        // core source-lineage requirement enforced by the database constraint.
        if (targetCount != 1) errors.Add("Select exactly one controlled maintenance asset or building/property.");

        if (request.WorkClassification == CivilEngineeringWorkClassification.ScheduledMaintenance)
        {
            if (request.Source != CivilEngineeringRequestSource.Maintenance) errors.Add("Scheduled maintenance intake must use the Maintenance source.");
            if (!request.MaintenanceScheduleId.HasValue) errors.Add("Select the originating controlled maintenance schedule.");
        }
        else if (request.MaintenanceScheduleId.HasValue)
            errors.Add("A maintenance schedule may be linked only to scheduled maintenance intake.");

        if (request.WorkClassification == CivilEngineeringWorkClassification.AssetComplaintResolution)
        {
            if (request.Source is not (CivilEngineeringRequestSource.TenantComplaint or CivilEngineeringRequestSource.Estate)) errors.Add("Company asset complaints must use the TenantComplaint or Estate source.");
            if (!request.HelpdeskTicketId.HasValue) errors.Add("Select the authoritative Helpdesk complaint ticket.");
        }
        else if (request.HelpdeskTicketId.HasValue)
            errors.Add("A Helpdesk complaint ticket may be linked only to company asset complaint intake.");

        return errors;
    }
}
