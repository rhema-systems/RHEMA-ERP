using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class ProjectUnitSalesSyncRules
{
    public static string ResolveStatusFromAgreement(
        string? currentStatus,
        bool isReleasedForMarket,
        SalesAgreementType agreementType,
        SalesAgreementStatus agreementStatus,
        SalesOrderType? linkedOrderType = null,
        SalesOrderStatus? linkedOrderStatus = null)
    {
        var normalizedCurrent = NormalizeStatus(currentStatus);
        if (IsTerminalUnitStatus(normalizedCurrent))
        {
            return normalizedCurrent;
        }

        if (linkedOrderStatus.HasValue)
        {
            return ResolveStatusFromOrder(
                normalizedCurrent,
                isReleasedForMarket,
                linkedOrderType ?? SalesOrderType.Standard,
                linkedOrderStatus.Value,
                agreementType,
                agreementStatus);
        }

        return ResolveStatusFromAgreementCore(isReleasedForMarket, agreementType, agreementStatus);
    }

    public static string ResolveStatusFromOrder(
        string? currentStatus,
        bool isReleasedForMarket,
        SalesOrderType orderType,
        SalesOrderStatus orderStatus,
        SalesAgreementType? linkedAgreementType = null,
        SalesAgreementStatus? linkedAgreementStatus = null)
    {
        var normalizedCurrent = NormalizeStatus(currentStatus);
        if (IsTerminalUnitStatus(normalizedCurrent))
        {
            return normalizedCurrent;
        }

        return orderStatus switch
        {
            SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced or SalesOrderStatus.Closed
                => orderType == SalesOrderType.LeaseAgreement
                    ? ProjectUnitStatuses.Leased
                    : ProjectUnitStatuses.Sold,
            SalesOrderStatus.PendingApproval or SalesOrderStatus.Draft or SalesOrderStatus.OnHold
                => ProjectUnitStatuses.Reserved,
            SalesOrderStatus.Rejected or SalesOrderStatus.Cancelled
                => linkedAgreementType.HasValue && linkedAgreementStatus.HasValue
                    ? ResolveStatusFromAgreementCore(isReleasedForMarket, linkedAgreementType.Value, linkedAgreementStatus.Value)
                    : ResolveStatusFromRelease(isReleasedForMarket),
            _ => normalizedCurrent
        };
    }

    private static string ResolveStatusFromAgreementCore(
        bool isReleasedForMarket,
        SalesAgreementType agreementType,
        SalesAgreementStatus agreementStatus)
    {
        if (IsLeaseAgreementType(agreementType))
        {
            return agreementStatus switch
            {
                SalesAgreementStatus.Active or SalesAgreementStatus.Expiring or SalesAgreementStatus.Renewed => ProjectUnitStatuses.Leased,
                SalesAgreementStatus.Terminated or SalesAgreementStatus.Expired => ResolveStatusFromRelease(isReleasedForMarket),
                _ => ProjectUnitStatuses.Reserved
            };
        }

        return agreementStatus switch
        {
            SalesAgreementStatus.Terminated or SalesAgreementStatus.Expired => ResolveStatusFromRelease(isReleasedForMarket),
            _ => ProjectUnitStatuses.Reserved
        };
    }

    private static bool IsTerminalUnitStatus(string status)
        => string.Equals(status, ProjectUnitStatuses.HandedOver, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectUnitStatuses.Occupied, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectUnitStatuses.Archived, StringComparison.OrdinalIgnoreCase);

    private static bool IsLeaseAgreementType(SalesAgreementType agreementType)
        => agreementType == SalesAgreementType.LeaseAgreement
            || agreementType == SalesAgreementType.TenancyAgreement;

    private static string ResolveStatusFromRelease(bool isReleasedForMarket)
        => isReleasedForMarket ? ProjectUnitStatuses.Available : ProjectUnitStatuses.Planned;

    private static string NormalizeStatus(string? currentStatus)
        => string.IsNullOrWhiteSpace(currentStatus)
            ? ProjectUnitStatuses.Planned
            : currentStatus.Trim();
}
