using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private async Task<ProjectPostHandoverSummaryDto> GetPostHandoverSummaryAsync(Guid projectId)
    {
        var handoverItems = await GetProjectHandoverItemEntitiesAsync(projectId);
        var defectCases = await GetProjectDefectLiabilityCaseEntitiesAsync(projectId);
        var units = (await GetProjectUnitEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var today = DateTime.UtcNow.Date;

        var openHandoverItemCount = handoverItems.Count(item =>
            !string.Equals(item.Status, ProjectHandoverItemStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.Status, ProjectHandoverItemStatuses.Waived, StringComparison.OrdinalIgnoreCase));

        var activeDefectCases = defectCases.Where(item =>
            !string.Equals(item.Status, ProjectDefectLiabilityStatuses.Resolved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.Status, ProjectDefectLiabilityStatuses.Closed, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.Status, ProjectDefectLiabilityStatuses.WarrantyExpired, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var alerts = new List<ProjectPostHandoverAlertDto>();

        foreach (var defect in activeDefectCases)
        {
            units.TryGetValue(defect.ProjectUnitId ?? Guid.Empty, out var unit);

            var responseDueDate = defect.ResponseSlaDays.HasValue
                ? defect.ReportedDate.Date.AddDays(defect.ResponseSlaDays.Value)
                : (DateTime?)null;
            var resolutionDueDate = defect.TargetResolutionDate?.Date
                ?? (defect.ResolutionSlaDays.HasValue ? defect.ReportedDate.Date.AddDays(defect.ResolutionSlaDays.Value) : null);

            if (responseDueDate.HasValue && !defect.FirstResponseDate.HasValue && responseDueDate.Value < today)
            {
                alerts.Add(CreatePostHandoverAlert(
                    "ResponseSlaBreach",
                    "High",
                    defect,
                    unit,
                    $"First response overdue for {defect.Title}",
                    $"No first response was logged by {responseDueDate:yyyy-MM-dd}.",
                    responseDueDate));
            }

            if (resolutionDueDate.HasValue && !defect.ResolvedDate.HasValue && resolutionDueDate.Value < today)
            {
                alerts.Add(CreatePostHandoverAlert(
                    "ResolutionSlaBreach",
                    "Critical",
                    defect,
                    unit,
                    $"Resolution overdue for {defect.Title}",
                    $"This defect passed its resolution due date on {resolutionDueDate:yyyy-MM-dd}.",
                    resolutionDueDate));
            }

            if (defect.IsWarrantyRelated && defect.WarrantyExpiryDate.HasValue)
            {
                var daysToExpiry = (defect.WarrantyExpiryDate.Value.Date - today).TotalDays;
                if (daysToExpiry <= 30)
                {
                    alerts.Add(CreatePostHandoverAlert(
                        "WarrantyExpiry",
                        daysToExpiry < 0 ? "Critical" : "Warning",
                        defect,
                        unit,
                        $"Warranty nearing expiry for {defect.Title}",
                        daysToExpiry < 0
                            ? $"The warranty expired on {defect.WarrantyExpiryDate:yyyy-MM-dd}."
                            : $"The warranty expires on {defect.WarrantyExpiryDate:yyyy-MM-dd}.",
                        defect.WarrantyExpiryDate));
                }
            }
        }

        return new ProjectPostHandoverSummaryDto
        {
            ProjectId = projectId,
            OpenHandoverItemCount = openHandoverItemCount,
            ActiveDefectLiabilityCount = activeDefectCases.Count,
            WarrantyCaseCount = defectCases.Count(item => item.IsWarrantyRelated),
            ChargeableCaseCount = defectCases.Count(item => !item.IsWarrantyRelated),
            ResponseBreachCount = alerts.Count(item => item.AlertType == "ResponseSlaBreach"),
            ResolutionBreachCount = alerts.Count(item => item.AlertType == "ResolutionSlaBreach"),
            WarrantyExpiringSoonCount = alerts.Count(item => item.AlertType == "WarrantyExpiry"),
            TotalRectificationExposure = defectCases.Sum(item => item.RectificationCost ?? 0m),
            ChargeableExposure = defectCases.Sum(item => item.ChargeableAmount ?? 0m),
            WarrantyExposure = defectCases.Where(item => item.IsWarrantyRelated).Sum(item => item.RectificationCost ?? 0m),
            Alerts = alerts
                .OrderByDescending(item => item.Severity == "Critical")
                .ThenBy(item => item.DueDate)
                .ThenBy(item => item.Title)
                .ToList()
        };
    }

    private static ProjectPostHandoverAlertDto CreatePostHandoverAlert(
        string alertType,
        string severity,
        ProjectDefectLiabilityCase defect,
        ProjectUnit? unit,
        string title,
        string message,
        DateTime? dueDate)
        => new()
        {
            AlertType = alertType,
            Severity = severity,
            ProjectUnitId = defect.ProjectUnitId,
            ProjectUnitCode = unit?.Code,
            ProjectUnitName = unit?.Name,
            ProjectDefectLiabilityCaseId = defect.Id,
            Title = title,
            Message = message,
            DueDate = dueDate
        };
}
