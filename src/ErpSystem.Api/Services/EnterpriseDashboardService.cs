using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Crm;
using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Crm;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ProcurementPurchaseOrderSummaryDto = ErpSystem.Core.DTOs.Procurement.PurchaseOrderSummaryDto;
using ProcurementPurchaseRequisitionSummaryDto = ErpSystem.Core.DTOs.Procurement.PurchaseRequisitionSummaryDto;
using TenderDto = ErpSystem.Core.DTOs.Procurement.TenderDto;

namespace ErpSystem.Api.Services;

public sealed class EnterpriseDashboardService
{
    private static readonly HashSet<string> ClosedPurchaseOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed",
        "Cancelled",
        "Closed"
    };

    private static readonly HashSet<string> ClosedTenderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed",
        "Cancelled",
        "Awarded",
        "Completed"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnterpriseDashboardService> _logger;

    public EnterpriseDashboardService(
        IServiceScopeFactory scopeFactory,
        ILogger<EnterpriseDashboardService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<EnterpriseDashboardDto> GetEnterpriseDashboardAsync(
        DateTime? requestedStartDate = null,
        DateTime? requestedEndDate = null,
        Guid? warehouseId = null,
        Guid? locationId = null)
    {
        var utcToday = DateTime.UtcNow.Date;
        var rangeEndDate = SpecifyUtcDate(requestedEndDate ?? utcToday);
        var rangeStartDate = SpecifyUtcDate(requestedStartDate ?? rangeEndDate.AddMonths(-5));
        var rangeEndExclusive = rangeEndDate.AddDays(1);
        var rangeEndInclusive = rangeEndExclusive.AddTicks(-1);
        var maintenanceScheduleHorizonDays = Math.Max(0, (rangeEndDate - utcToday).Days);

        var crmOverviewTask = RunModuleAsync(
            "CRM Overview",
            async sp => await sp.GetRequiredService<ICrmService>().GetOverviewAsync(8),
            fallback: (CrmOverviewDto?)null,
            fallbackMessage: "CRM overview is unavailable");

        var crmReportingTask = RunModuleAsync(
            "CRM Reporting",
            async sp => await sp.GetRequiredService<ICrmService>().GetReportingAsync(8),
            fallback: (CrmReportingDto?)null,
            fallbackMessage: "CRM reporting is unavailable");

        var crmConversionsTask = RunModuleAsync(
            "CRM Conversions",
            async sp => await sp.GetRequiredService<ICrmService>().GetConversionsAsync(months: 6),
            fallback: (CrmConversionsDto?)null,
            fallbackMessage: "CRM conversions are unavailable");

        var projectDashboardTask = RunModuleAsync(
            "Projects",
            async sp => await sp.GetRequiredService<IProjectService>().GetDashboardAsync(),
            fallback: (ProjectDashboardDto?)null,
            fallbackMessage: "Project dashboard is unavailable");

        var purchaseRequisitionsTask = RunModuleAsync(
            "Purchase Requisitions",
            async sp =>
            {
                var repository = sp.GetRequiredService<IPurchaseRequisitionRepository>();
                var requisitions = await repository.GetPendingApprovalRequisitions();
                return requisitions
                    .Where(requisition => IsWithinRange(requisition.RequisitionDate, rangeStartDate, rangeEndExclusive))
                    .Select(MapPurchaseRequisitionSummary)
                    .ToList();
            },
            fallback: new List<ProcurementPurchaseRequisitionSummaryDto>(),
            fallbackMessage: "Pending purchase requisitions are unavailable");

        var purchaseOrdersTask = RunModuleAsync(
            "Purchase Orders",
            async sp =>
            {
                var repository = sp.GetRequiredService<IPurchaseOrderRepository>();
                var orders = await repository.GetPurchaseOrdersAsync(
                    1,
                    250,
                    startDate: rangeStartDate,
                    endDate: rangeEndInclusive);
                return orders.Items
                    .Where(order => !ClosedPurchaseOrderStatuses.Contains(order.Status ?? string.Empty))
                    .Select(MapPurchaseOrderSummary)
                    .ToList();
            },
            fallback: new List<ProcurementPurchaseOrderSummaryDto>(),
            fallbackMessage: "Purchase order data is unavailable");

        var inventoryApprovalTask = RunModuleAsync(
            "Inventory Approval Queue",
            async sp => (await sp.GetRequiredService<IInventoryRequisitionService>().GetPendingApprovalAsync())
                .Where(requisition => IsWithinRange(requisition.RequestDate, rangeStartDate, rangeEndExclusive))
                .ToList(),
            fallback: new List<InventoryRequisitionDto>(),
            fallbackMessage: "Pending inventory approvals are unavailable");

        var inventoryIssueTask = RunModuleAsync(
            "Inventory Issue Queue",
            async sp => (await sp.GetRequiredService<IInventoryRequisitionService>().GetPendingIssueAsync())
                .Where(requisition => IsWithinRange(requisition.RequestDate, rangeStartDate, rangeEndExclusive))
                .ToList(),
            fallback: new List<InventoryRequisitionDto>(),
            fallbackMessage: "Pending inventory issues are unavailable");

        var maintenanceOverviewTask = RunModuleAsync(
            "Maintenance Overview",
            async sp => await sp.GetRequiredService<IMaintenanceAnalyticsService>().GetDashboardDataAsync(),
            fallback: (MaintenanceDashboardDto?)null,
            fallbackMessage: "Maintenance overview is unavailable");

        var maintenanceMetricsTask = RunModuleAsync(
            "Maintenance Metrics",
            async sp => await sp.GetRequiredService<IWorkOrderService>().GetWorkOrderMetricsAsync(rangeStartDate, rangeEndInclusive),
            fallback: (WorkOrderMetricsDto?)null,
            fallbackMessage: "Maintenance work order metrics are unavailable");

        var maintenanceTrendsTask = RunModuleAsync(
            "Maintenance Trends",
            async sp => await sp.GetRequiredService<IMaintenanceAnalyticsService>().GetWorkOrderTrendsAsync(rangeStartDate, rangeEndInclusive),
            fallback: (WorkOrderTrendsDto?)null,
            fallbackMessage: "Maintenance trends are unavailable");

        var upcomingMaintenanceTask = RunModuleAsync(
            "Upcoming Maintenance",
            async sp =>
            {
                var schedules = await sp.GetRequiredService<IMaintenanceScheduleService>()
                    .GetSchedulesDueInDaysAsync(maintenanceScheduleHorizonDays);
                return schedules
                    .Select(MapMaintenanceSchedule)
                    .Where(schedule => GetScheduleDate(schedule) is DateTime scheduleDate
                        && IsWithinRange(scheduleDate, rangeStartDate, rangeEndExclusive))
                    .ToList();
            },
            fallback: new List<EnterpriseMaintenanceScheduleDto>(),
            fallbackMessage: "Upcoming maintenance is unavailable");

        var tendersTask = RunModuleAsync(
            "Tenders",
            async sp =>
            {
                var response = await sp.GetRequiredService<ITenderService>().GetTendersAsync(1, 250);
                return response.Items
                    .Where(tender => IsWithinRange(tender.PublishDate ?? tender.CreatedAt, rangeStartDate, rangeEndExclusive))
                    .Where(tender => !ClosedTenderStatuses.Contains(tender.Status ?? string.Empty))
                    .ToList();
            },
            fallback: new List<TenderDto>(),
            fallbackMessage: "Tender data is unavailable");

        var procurementInventoryManagementTask = RunModuleAsync(
            "Procurement and Inventory Management",
            async sp => await sp.GetRequiredService<ProcurementInventoryManagementDashboardService>()
                .GetAsync(rangeStartDate, rangeEndDate, warehouseId, locationId),
            fallback: (ProcurementInventoryManagementDashboardDto?)null,
            fallbackMessage: "Procurement and inventory management metrics are unavailable");

        await Task.WhenAll(
            crmOverviewTask,
            crmReportingTask,
            crmConversionsTask,
            projectDashboardTask,
            purchaseRequisitionsTask,
            purchaseOrdersTask,
            inventoryApprovalTask,
            inventoryIssueTask,
            maintenanceOverviewTask,
            maintenanceMetricsTask,
            maintenanceTrendsTask,
            upcomingMaintenanceTask,
            tendersTask,
            procurementInventoryManagementTask);

        return new EnterpriseDashboardDto
        {
            CrmOverview = crmOverviewTask.Result.Data,
            CrmReporting = crmReportingTask.Result.Data,
            CrmConversions = crmConversionsTask.Result.Data,
            ProjectDashboard = projectDashboardTask.Result.Data,
            PendingPurchaseRequisitions = purchaseRequisitionsTask.Result.Data,
            OpenPurchaseOrders = purchaseOrdersTask.Result.Data,
            PendingInventoryApprovals = inventoryApprovalTask.Result.Data,
            PendingInventoryIssues = inventoryIssueTask.Result.Data,
            MaintenanceOverview = maintenanceOverviewTask.Result.Data,
            MaintenanceMetrics = maintenanceMetricsTask.Result.Data,
            MaintenanceTrends = maintenanceTrendsTask.Result.Data,
            UpcomingMaintenance = upcomingMaintenanceTask.Result.Data,
            Tenders = tendersTask.Result.Data,
            ProcurementInventoryManagement = procurementInventoryManagementTask.Result.Data,
            RangeStartDate = rangeStartDate,
            RangeEndDate = rangeEndDate,
            ModuleStatus =
            [
                crmOverviewTask.Result.Status,
                crmReportingTask.Result.Status,
                crmConversionsTask.Result.Status,
                projectDashboardTask.Result.Status,
                purchaseRequisitionsTask.Result.Status,
                purchaseOrdersTask.Result.Status,
                inventoryApprovalTask.Result.Status,
                inventoryIssueTask.Result.Status,
                maintenanceOverviewTask.Result.Status,
                maintenanceMetricsTask.Result.Status,
                maintenanceTrendsTask.Result.Status,
                upcomingMaintenanceTask.Result.Status,
                tendersTask.Result.Status,
                procurementInventoryManagementTask.Result.Status
            ],
            LastUpdated = DateTime.UtcNow
        };
    }

    private static DateTime SpecifyUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static bool IsWithinRange(DateTime value, DateTime startDate, DateTime endExclusive) =>
        value >= startDate && value < endExclusive;

    private static DateTime? GetScheduleDate(EnterpriseMaintenanceScheduleDto schedule) =>
        schedule.NextDue ?? schedule.NextDueDate ?? schedule.NextScheduledDate;

    private async Task<ModuleResult<T>> RunModuleAsync<T>(
        string module,
        Func<IServiceProvider, Task<T>> action,
        T fallback,
        string fallbackMessage)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var data = await action(scope.ServiceProvider);

            return new ModuleResult<T>(
                data,
                new EnterpriseDashboardModuleStatusDto
                {
                    Module = module,
                    Available = true
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Enterprise dashboard module {Module} failed", module);

            return new ModuleResult<T>(
                fallback,
                new EnterpriseDashboardModuleStatusDto
                {
                    Module = module,
                    Available = false,
                    Error = ex.Message
                });
        }
    }

    private static ProcurementPurchaseOrderSummaryDto MapPurchaseOrderSummary(Core.Entities.Procurement.PurchaseOrder purchaseOrder)
    {
        return new ProcurementPurchaseOrderSummaryDto
        {
            Id = purchaseOrder.Id,
            OrderNumber = purchaseOrder.OrderNumber,
            OrderType = purchaseOrder.OrderType,
            SupplierId = purchaseOrder.BusinessPartnerId,
            SupplierName = purchaseOrder.BusinessPartner?.PartnerName ?? string.Empty,
            OrderDate = purchaseOrder.OrderDate,
            RequiredDate = purchaseOrder.RequiredDate,
            PromisedDate = purchaseOrder.PromisedDate,
            Status = purchaseOrder.Status,
            TotalAmount = purchaseOrder.TotalAmount,
            ItemCount = purchaseOrder.Items?.Count ?? 0,
            RequestedByName = string.Join(' ', new[] { purchaseOrder.RequestedBy?.FirstName, purchaseOrder.RequestedBy?.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
        };
    }

    private static ProcurementPurchaseRequisitionSummaryDto MapPurchaseRequisitionSummary(Core.Entities.Procurement.PurchaseRequisition requisition)
    {
        return new ProcurementPurchaseRequisitionSummaryDto
        {
            Id = requisition.Id,
            RequisitionNumber = requisition.RequisitionNumber,
            RequisitionDate = requisition.RequisitionDate,
            RequestedByName = string.Join(' ', new[] { requisition.RequestedBy?.FirstName, requisition.RequestedBy?.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            RequiredDate = requisition.RequiredDate,
            Status = requisition.Status,
            Priority = requisition.Priority,
            Department = requisition.Department,
            TotalAmount = requisition.TotalAmount,
            ItemCount = requisition.Items?.Count ?? 0
        };
    }

    private static EnterpriseMaintenanceScheduleDto MapMaintenanceSchedule(MaintenanceScheduleDto schedule)
    {
        return new EnterpriseMaintenanceScheduleDto
        {
            Id = schedule.Id,
            AssetName = schedule.AssetName,
            MaintenanceTypeName = schedule.MaintenanceTypeName,
            MaintenanceType = schedule.MaintenanceType,
            Name = schedule.Name,
            NextDue = schedule.NextDue,
            NextDueDate = schedule.NextDueDate,
            NextScheduledDate = schedule.NextScheduledDate,
            AssignedTechnicianName = schedule.AssignedTechnicianName
        };
    }

    private sealed record ModuleResult<T>(T Data, EnterpriseDashboardModuleStatusDto Status);
}
