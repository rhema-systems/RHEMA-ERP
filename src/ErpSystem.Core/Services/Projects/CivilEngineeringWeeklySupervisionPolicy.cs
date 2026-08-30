using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringWeeklySupervisionPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringWeeklySupervisionReportRequest request, CivilEngineeringWeeklyReportValue policy, DateTime now)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        var weekStart = request.WeekStart.Date;
        if (weekStart == default || weekStart.DayOfWeek != DayOfWeek.Monday) errors.Add("Weekly supervision reports must start on a Monday.");
        if (request.ProjectMilestoneId == Guid.Empty) errors.Add("Select the controlled project milestone covered by this report.");
        if (!CivilEngineeringWeeklySupervisionSiteStatuses.All.Contains(request.SiteStatus?.Trim() ?? string.Empty, StringComparer.Ordinal)) errors.Add("Select a controlled site status.");
        if (!request.OverallProgressPercent.HasValue) errors.Add("Enter the overall progress percentage for this reporting period.");
        if (CivilEngineeringWeeklySupervisionSiteStatuses.RequiresRecoveryAction(request.SiteStatus))
        {
            if (string.IsNullOrWhiteSpace(request.DelayReason) || request.DelayReason.Trim().Length < 3) errors.Add("Provide the delay or risk reason.");
            if (string.IsNullOrWhiteSpace(request.RecoveryAction) || request.RecoveryAction.Trim().Length < 3) errors.Add("Provide the recovery action.");
            if (!request.RecoveryOwnerUserId.HasValue || request.RecoveryOwnerUserId == Guid.Empty) errors.Add("Select the responsible recovery-action owner.");
            if (!request.RecoveryDueDate.HasValue) errors.Add("Select the recovery-action due date.");
            else if (request.RecoveryDueDate.Value.Date < weekStart) errors.Add("The recovery-action due date cannot be before the reporting week.");
        }
        if (weekStart > now.Date) errors.Add("A weekly supervision report cannot start in the future.");
        if (policy.RequireProgressMeasurement && !request.OverallProgressPercent.HasValue) errors.Add("Overall progress is required by CIV-CFG-006.");
        if (request.OverallProgressPercent is < 0 or > 100) errors.Add("Overall progress must be between 0 and 100.");
        RequiredWhen(policy.RequireMaterialUsage, request.MaterialUsageSummary, "Material-usage summary", errors);
        RequiredWhen(policy.RequireSafetyNotes, request.SafetyNotes, "Safety notes", errors);
        RequiredWhen(policy.RequireTestSummary, request.TestSummary, "Test summary", errors);
        if (request.Activities is null || request.Activities.Count == 0) errors.Add("Add at least one governed contractor or labour-gang activity.");
        if (request.Activities?.Count > 100) errors.Add("A weekly supervision report may contain at most 100 activities.");
        foreach (var activity in request.Activities ?? [])
        {
            if (activity.ActivityCategoryId == Guid.Empty || !policy.ActivityCategoryIds.Contains(activity.ActivityCategoryId)) errors.Add("Each activity must select a category permitted by CIV-CFG-006.");
            if (!CivilEngineeringWeeklySupervisionActorTypes.All.Contains(activity.ActorType?.Trim() ?? string.Empty, StringComparer.Ordinal)) errors.Add("Each activity must select Contractor or LabourGang.");
            if (string.Equals(activity.ActorType?.Trim(), CivilEngineeringWeeklySupervisionActorTypes.Contractor, StringComparison.Ordinal) && !activity.ContractorBusinessPartnerId.HasValue) errors.Add("A contractor activity must select its controlled Business Partner.");
            if (string.Equals(activity.ActorType?.Trim(), CivilEngineeringWeeklySupervisionActorTypes.LabourGang, StringComparison.Ordinal) && activity.ContractorBusinessPartnerId.HasValue) errors.Add("A labour-gang activity cannot select an external Business Partner.");
            if (string.IsNullOrWhiteSpace(activity.Description) || activity.Description.Trim().Length < 3) errors.Add("Each activity needs a narrative description.");
            if (activity.ProgressPercent is < 0 or > 100) errors.Add("Activity progress must be between 0 and 100.");
            if (policy.RequireProgressMeasurement && !activity.ProgressPercent.HasValue) errors.Add("Each activity requires a progress measurement under CIV-CFG-006.");
        }
        var evidence = request.Evidence ?? [];
        if (evidence.Count == 0) errors.Add("Select current published central-DMS report evidence.");
        if (evidence.GroupBy(value => value.CentralDocumentVersionId).Any(group => group.Count() > 1)) errors.Add("Each evidence version can be linked only once.");
        foreach (var item in evidence)
        {
            if (item.CentralDocumentRecordId == Guid.Empty || item.CentralDocumentVersionId == Guid.Empty) errors.Add("Each evidence item requires both a central DMS document and version.");
            if (!CivilEngineeringWeeklySupervisionEvidenceRoles.All.Contains(item.EvidenceRole?.Trim() ?? string.Empty, StringComparer.Ordinal)) errors.Add("Select a valid report evidence role.");
        }
        if (evidence.Count(value => string.Equals(value.EvidenceRole?.Trim(), CivilEngineeringWeeklySupervisionEvidenceRoles.Photo, StringComparison.Ordinal)) < policy.MinimumPhotoCount) errors.Add($"CIV-CFG-006 requires at least {policy.MinimumPhotoCount} photo evidence item(s).");
        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> ValidateReview(ProcessCivilEngineeringWeeklySupervisionReportRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion)) errors.Add("Refresh the weekly report before deciding it.");
        if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Trim().Length < 3) errors.Add("A review or return comment is required.");
        if (request.CentralDocumentRecordId.HasValue != request.CentralDocumentVersionId.HasValue) errors.Add("Select both the DMS review document and version, or neither.");
        return errors;
    }

    private static void RequiredWhen(bool required, string? value, string label, ICollection<string> errors)
    {
        if (required && string.IsNullOrWhiteSpace(value)) errors.Add($"{label} is required by CIV-CFG-006.");
        if (value?.Trim().Length > 4000) errors.Add($"{label} cannot exceed 4000 characters.");
    }
}
