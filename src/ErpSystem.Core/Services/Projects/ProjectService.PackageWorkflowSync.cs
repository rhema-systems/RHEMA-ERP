using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    private async Task<int> ApplyPhasePackageStatusNudgesAsync(ProjectPhase phase, bool phaseCompleted)
    {
        var packages = (await GetProjectPackageEntitiesAsync(phase.ProjectId))
            .Where(item => item.ProjectPhaseId == phase.Id)
            .ToList();

        if (packages.Count == 0)
        {
            return 0;
        }

        var changed = new List<ProjectPackage>();
        foreach (var package in packages)
        {
            if (IsProjectPackageHoldOrTerminalStatus(package.Status))
            {
                continue;
            }

            var targetStatus = ResolvePackageNudgeStatusForPhase(phase, package, phaseCompleted);
            if (string.IsNullOrWhiteSpace(targetStatus)
                || string.Equals(package.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            package.Status = targetStatus;
            package.UpdatedBy = _currentUserProvider.Username;
            package.LastModifiedById = _currentUserProvider.UserId;
            changed.Add(package);
        }

        if (changed.Count > 0)
        {
            await _unitOfWork.Repository<ProjectPackage>().UpdateRangeAsync(changed);
        }

        return changed.Count;
    }

    private static string? ResolvePackageNudgeStatusForPhase(ProjectPhase phase, ProjectPackage package, bool phaseCompleted)
    {
        if (IsProcurementPhase(phase))
        {
            if (phaseCompleted)
            {
                return HasPackageAwardSignal(package) && GetProjectPackageStatusRank(package.Status) < GetProjectPackageStatusRank(ProjectPackageStatuses.Awarded)
                    ? ProjectPackageStatuses.Awarded
                    : null;
            }

            return GetProjectPackageStatusRank(package.Status) < GetProjectPackageStatusRank(ProjectPackageStatuses.ProcurementPending)
                ? ProjectPackageStatuses.ProcurementPending
                : null;
        }

        if (IsConstructionPhase(phase) && !phaseCompleted)
        {
            if (HasPackageActivationSignal(package))
            {
                return GetProjectPackageStatusRank(package.Status) < GetProjectPackageStatusRank(ProjectPackageStatuses.Active)
                    ? ProjectPackageStatuses.Active
                    : null;
            }

            if (HasPackageProcurementSignal(package))
            {
                return GetProjectPackageStatusRank(package.Status) < GetProjectPackageStatusRank(ProjectPackageStatuses.ProcurementPending)
                    ? ProjectPackageStatuses.ProcurementPending
                    : null;
            }
        }

        return null;
    }

    private static bool IsPackageCommerciallyAlignedToPhase(ProjectPhase phase, ProjectPackage package)
    {
        if (IsProjectPackageHoldOrTerminalStatus(package.Status))
        {
            return true;
        }

        var minimumRank = GetMinimumPackageStatusRankForPhase(phase, package);
        if (!minimumRank.HasValue)
        {
            return true;
        }

        return GetProjectPackageStatusRank(package.Status) >= minimumRank.Value;
    }

    private static string ResolvePackagePhaseCommercialSyncStatus(ProjectPhase? phase, ProjectPackage package)
    {
        if (phase == null)
        {
            return "Unassigned";
        }

        if (IsProjectPackageHoldOrTerminalStatus(package.Status))
        {
            return "Stable";
        }

        var minimumRank = GetMinimumPackageStatusRankForPhase(phase, package);
        if (!minimumRank.HasValue)
        {
            return "Watching";
        }

        return IsPackageCommerciallyAlignedToPhase(phase, package) ? "Aligned" : "Lagging";
    }

    private static string? BuildPackagePhaseCommercialSyncMessage(ProjectPhase? phase, ProjectPackage package)
    {
        if (phase == null)
        {
            return "Assign this package to a project phase so the system can guide its procurement and delivery posture.";
        }

        if (IsProjectPackageHoldOrTerminalStatus(package.Status))
        {
            return $"Package is {package.Status} and is excluded from automatic phase nudges.";
        }

        var minimumRank = GetMinimumPackageStatusRankForPhase(phase, package);
        if (!minimumRank.HasValue)
        {
            return "Current phase posture does not require a package workflow nudge yet.";
        }

        if (IsPackageCommerciallyAlignedToPhase(phase, package))
        {
            return "Package status is aligned with the current phase posture.";
        }

        var targetStatus = ResolvePackageNudgeStatusForPhase(
            phase,
            package,
            string.Equals(phase.Status, ProjectPhaseStatuses.Completed, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(targetStatus))
        {
            return $"Package is behind its linked phase. Move it toward {targetStatus} or complete the missing commercial linkage.";
        }

        return "Package is behind its linked phase and needs commercial follow-through.";
    }

    private static string? ResolveRecommendedNextStatus(ProjectPhase? phase, ProjectPackage package)
    {
        if (phase == null || IsProjectPackageHoldOrTerminalStatus(package.Status))
        {
            return null;
        }

        if (IsPackageCommerciallyAlignedToPhase(phase, package))
        {
            return null;
        }

        return ResolvePackageNudgeStatusForPhase(
            phase,
            package,
            string.Equals(phase.Status, ProjectPhaseStatuses.Completed, StringComparison.OrdinalIgnoreCase));
    }

    private static string? BuildRecommendedPackageNextAction(ProjectPhase? phase, ProjectPackage package)
    {
        if (phase == null)
        {
            return "Assign the package to the right project phase first.";
        }

        if (IsProjectPackageHoldOrTerminalStatus(package.Status) || IsPackageCommerciallyAlignedToPhase(phase, package))
        {
            return null;
        }

        if (IsProcurementPhase(phase))
        {
            if (string.Equals(phase.Status, ProjectPhaseStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            {
                return HasPackageAwardSignal(package)
                    ? "Promote this package to Awarded to reflect the finished procurement outcome."
                    : "Complete award, contract, PO, or vendor linkage so procurement closeout is commercially visible.";
            }

            return HasPackageProcurementSignal(package)
                ? "Promote this package to Procurement Pending so active procurement work is visible."
                : "Link a procurement plan item, PR, tender, or sourcing path before procurement progresses further.";
        }

        if (IsConstructionPhase(phase))
        {
            return HasPackageActivationSignal(package)
                ? "Promote this package to Active so live construction delivery is reflected."
                : "Link a contract, PO, or executing partner before this package can be treated as active on site.";
        }

        return "Review this package against its current phase and complete the missing commercial follow-through.";
    }

    private static int? GetMinimumPackageStatusRankForPhase(ProjectPhase phase, ProjectPackage package)
    {
        if (string.Equals(phase.Status, ProjectPhaseStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
        {
            if (IsProcurementPhase(phase))
            {
                return GetProjectPackageStatusRank(ProjectPackageStatuses.ProcurementPending);
            }

            if (IsConstructionPhase(phase))
            {
                return GetProjectPackageStatusRank(ProjectPackageStatuses.Active);
            }
        }

        if (string.Equals(phase.Status, ProjectPhaseStatuses.Completed, StringComparison.OrdinalIgnoreCase) && IsProcurementPhase(phase))
        {
            return HasPackageAwardSignal(package)
                ? GetProjectPackageStatusRank(ProjectPackageStatuses.Awarded)
                : GetProjectPackageStatusRank(ProjectPackageStatuses.ProcurementPending);
        }

        return null;
    }

    private static bool IsProcurementPhase(ProjectPhase phase)
    {
        var marker = BuildProjectPhaseStatusMarker(phase);
        return marker.Contains("PROCUREMENT", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConstructionPhase(ProjectPhase phase)
    {
        var marker = BuildProjectPhaseStatusMarker(phase);
        return marker.Contains("CONSTRUCTION", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildProjectPhaseStatusMarker(ProjectPhase phase)
        => $"{phase.Code} {phase.Name}".Trim();

    private static bool HasPackageProcurementSignal(ProjectPackage package)
        => package.ProcurementPlanItemId.HasValue
            || package.PurchaseRequisitionId.HasValue
            || package.TenderId.HasValue
            || HasPackageAwardSignal(package);

    private static bool HasPackageAwardSignal(ProjectPackage package)
        => package.ContractId.HasValue
            || package.PurchaseOrderId.HasValue
            || package.BusinessPartnerId.HasValue;

    private static bool HasPackageActivationSignal(ProjectPackage package)
        => package.ContractId.HasValue
            || package.PurchaseOrderId.HasValue
            || package.BusinessPartnerId.HasValue;

    private static bool IsProjectPackageHoldOrTerminalStatus(string? status)
        => string.Equals(status, ProjectPackageStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectPackageStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectPackageStatuses.OnHold, StringComparison.OrdinalIgnoreCase);

    private static int GetProjectPackageStatusRank(string? status)
        => status?.Trim() switch
        {
            ProjectPackageStatuses.Planned => 0,
            ProjectPackageStatuses.ProcurementPending => 1,
            ProjectPackageStatuses.Awarded => 2,
            ProjectPackageStatuses.Active => 3,
            ProjectPackageStatuses.Completed => 4,
            ProjectPackageStatuses.OnHold => 90,
            ProjectPackageStatuses.Cancelled => 99,
            _ => 0
        };
}
