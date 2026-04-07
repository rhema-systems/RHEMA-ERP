using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class ProjectUnitPresentationRules
{
    public static SalesLinkedProjectUnitContextDto BuildSalesLinkedProjectUnitContext(
        ProjectUnit unit,
        Project? project = null,
        SalesAgreement? salesAgreement = null,
        SalesOrder? salesOrder = null)
    {
        project ??= unit.Project;
        salesAgreement ??= unit.SalesAgreement;
        salesOrder ??= unit.SalesOrder;

        var commercialStatus = DeriveCommercialStatus(unit, salesAgreement, salesOrder);
        var handoverStatus = DeriveHandoverStatus(unit, commercialStatus);

        return new SalesLinkedProjectUnitContextDto
        {
            ProjectId = unit.ProjectId,
            ProjectCode = project?.ProjectCode ?? string.Empty,
            ProjectTitle = project?.Title ?? string.Empty,
            ProjectUnitId = unit.Id,
            ProjectUnitCode = unit.Code,
            ProjectUnitName = unit.Name,
            ProjectUnitType = unit.UnitType,
            ProjectUnitStatus = unit.Status,
            ProjectUnitCommercialStatus = commercialStatus,
            ProjectUnitHandoverStatus = handoverStatus,
            IsReleasedForMarket = unit.IsReleasedForMarket,
            HandoverDate = unit.HandoverDate
        };
    }

    public static string DeriveCommercialStatus(ProjectUnit unit, SalesAgreement? salesAgreement, SalesOrder? salesOrder)
    {
        if (StatusEquals(unit.Status, ProjectUnitStatuses.Archived))
        {
            return ProjectUnitStatuses.Archived;
        }

        if (StatusEquals(unit.Status, ProjectUnitStatuses.Occupied))
        {
            return ProjectUnitStatuses.Occupied;
        }

        if (StatusEquals(unit.Status, ProjectUnitStatuses.HandedOver))
        {
            return ProjectUnitStatuses.HandedOver;
        }

        if (salesOrder != null)
        {
            return salesOrder.OrderStatus switch
            {
                SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced or SalesOrderStatus.Closed => ResolveTransactedCommercialStatus(unit.Status, salesOrder.OrderType),
                SalesOrderStatus.PendingApproval or SalesOrderStatus.Draft or SalesOrderStatus.OnHold => ProjectUnitStatuses.Reserved,
                _ => FallbackCommercialStatus(unit, salesAgreement)
            };
        }

        if (StatusEquals(unit.Status, ProjectUnitStatuses.Leased))
        {
            return ProjectUnitStatuses.Leased;
        }

        if (StatusEquals(unit.Status, ProjectUnitStatuses.Sold))
        {
            return ProjectUnitStatuses.Sold;
        }

        return FallbackCommercialStatus(unit, salesAgreement);
    }

    public static string DeriveHandoverStatus(ProjectUnit unit, string commercialStatus)
    {
        if (StatusEquals(unit.Status, ProjectUnitStatuses.Occupied))
        {
            return ProjectUnitHandoverStatuses.Occupied;
        }

        if (StatusEquals(unit.Status, ProjectUnitStatuses.HandedOver))
        {
            return ProjectUnitHandoverStatuses.HandedOver;
        }

        if (unit.HandoverDate.HasValue)
        {
            return unit.HandoverDate.Value.Date > DateTime.UtcNow.Date
                ? ProjectUnitHandoverStatuses.Scheduled
                : ProjectUnitHandoverStatuses.Due;
        }

        if (StatusEquals(commercialStatus, ProjectUnitStatuses.Sold)
            || StatusEquals(commercialStatus, ProjectUnitStatuses.Leased)
            || StatusEquals(commercialStatus, ProjectUnitStatuses.Reserved))
        {
            return ProjectUnitHandoverStatuses.Pending;
        }

        return ProjectUnitHandoverStatuses.NotScheduled;
    }

    public static bool IsLeaseAgreementType(SalesAgreementType agreementType)
        => agreementType == SalesAgreementType.LeaseAgreement
            || agreementType == SalesAgreementType.TenancyAgreement;

    public static bool StatusEquals(string? left, string right)
        => string.Equals(left?.Trim(), right, StringComparison.OrdinalIgnoreCase);

    public static string AlignStatusForRelease(string? status, bool isReleasedForMarket)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? ProjectUnitStatuses.Planned : status.Trim();
        if (isReleasedForMarket && StatusEquals(normalizedStatus, ProjectUnitStatuses.Planned))
        {
            return ProjectUnitStatuses.Available;
        }

        if (!isReleasedForMarket && StatusEquals(normalizedStatus, ProjectUnitStatuses.Available))
        {
            return ProjectUnitStatuses.Planned;
        }

        return normalizedStatus;
    }

    private static string FallbackCommercialStatus(ProjectUnit unit, SalesAgreement? salesAgreement)
    {
        if (salesAgreement != null)
        {
            if (IsLeaseAgreementType(salesAgreement.AgreementType))
            {
                return salesAgreement.AgreementStatus switch
                {
                    SalesAgreementStatus.Active or SalesAgreementStatus.Expiring or SalesAgreementStatus.Renewed => ProjectUnitStatuses.Leased,
                    SalesAgreementStatus.PendingApproval or SalesAgreementStatus.Draft or SalesAgreementStatus.Suspended => ProjectUnitStatuses.Reserved,
                    _ => AlignStatusForRelease(unit.Status, unit.IsReleasedForMarket)
                };
            }

            return salesAgreement.AgreementStatus switch
            {
                SalesAgreementStatus.Active or SalesAgreementStatus.PendingApproval or SalesAgreementStatus.Expiring or SalesAgreementStatus.Renewed or SalesAgreementStatus.Suspended => ProjectUnitStatuses.Reserved,
                _ => AlignStatusForRelease(unit.Status, unit.IsReleasedForMarket)
            };
        }

        return AlignStatusForRelease(unit.Status, unit.IsReleasedForMarket);
    }

    private static string ResolveTransactedCommercialStatus(string? currentStatus, SalesOrderType? orderType = null)
        => orderType == SalesOrderType.LeaseAgreement || StatusEquals(currentStatus, ProjectUnitStatuses.Leased)
            ? ProjectUnitStatuses.Leased
            : ProjectUnitStatuses.Sold;
}
