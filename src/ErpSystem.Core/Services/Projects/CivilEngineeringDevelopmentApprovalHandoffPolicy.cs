using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringDevelopmentApprovalHandoffPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringDevelopmentApprovalHandoffRequest request, CivilEngineeringPermittingSection fromSection, DateTime nowUtc)
    {
        var errors = new List<string>();
        var evidence = request.Evidence ?? [];
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (!Enum.IsDefined(request.ToSection)) errors.Add("Select a supported destination section.");
        if (request.RecipientRoleId == Guid.Empty) errors.Add("Select a configured recipient role.");
        if (request.RecipientUserId == Guid.Empty) errors.Add("Select an active recipient user.");
        if (request.ToSection == fromSection) errors.Add("Select a different destination section.");
        if (request.DueDate == default) errors.Add("Select a due date for the recipient section.");
        else if (request.DueDate.Date < nowUtc.Date) errors.Add("The handoff due date cannot be in the past.");
        if (request.CoverNote?.Trim().Length > 2000) errors.Add("The handoff cover note cannot exceed 2,000 characters.");
        if (evidence.Any(item => item.CentralDocumentRecordId == Guid.Empty || item.CentralDocumentVersionId == Guid.Empty)) errors.Add("Each selected handoff evidence item must identify a central-DMS document record and version.");
        if (evidence.Select(item => item.CentralDocumentVersionId).Distinct().Count() != evidence.Count) errors.Add("Each handoff document version may be selected once.");
        return errors;
    }
}
