using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;

namespace ErpSystem.Api.Services;

public sealed class EnterpriseDashboardService
{
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

        var reportingCurrencyTask = RunModuleAsync(
            "Reporting Currency",
            async sp => await sp.GetRequiredService<ITenantSettingsService>().GetBaseCurrencyReferenceAsync(),
            fallback: new Core.DTOs.Finance.BaseCurrencyReferenceDto
            {
                CurrencyCode = string.Empty,
                CurrencyName = string.Empty,
                CurrencySymbol = string.Empty,
                DecimalPlaces = 0
            },
            fallbackMessage: "Reporting currency is unavailable");

        var financeTask = RunAuthorizedModuleAsync(
            "Finance",
            FinancePermissions.ViewFinance,
            async sp => await sp.GetRequiredService<IGeneralLedgerService>()
                .GetFinanceDashboardAsync(rangeStartDate, rangeEndDate),
            fallback: (Core.DTOs.Finance.FinanceDashboardDto?)null,
            fallbackMessage: "Finance analytics are unavailable");

        var crmTask = RunAuthorizedModuleAsync(
            "CRM",
            "Sales",
            async sp => await sp.GetRequiredService<EnterpriseDashboardProjectionService>()
                .GetCrmAsync(rangeStartDate, rangeEndExclusive),
            fallback: (EnterpriseCrmDashboardDto?)null,
            fallbackMessage: "CRM analytics are unavailable");

        var projectDashboardTask = RunModuleAsync(
            "Projects",
            async sp => await sp.GetRequiredService<IProjectService>().GetDashboardAsync(),
            fallback: (ProjectDashboardDto?)null,
            fallbackMessage: "Project dashboard is unavailable");

        var procurementQueuesTask = RunAuthorizedModuleAsync(
            "Procurement Queues",
            "Procurement",
            async sp => await sp.GetRequiredService<EnterpriseDashboardProjectionService>()
                .GetProcurementQueuesAsync(rangeStartDate, rangeEndExclusive),
            fallback: new EnterpriseOperationalQueueDto(),
            fallbackMessage: "Procurement queues are unavailable");

        var inventoryQueuesTask = RunAuthorizedModuleAsync(
            "Inventory Queues",
            "Inventory",
            async sp => await sp.GetRequiredService<EnterpriseDashboardProjectionService>()
                .GetInventoryQueuesAsync(rangeStartDate, rangeEndExclusive),
            fallback: new EnterpriseOperationalQueueDto(),
            fallbackMessage: "Inventory queues are unavailable");

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

        var procurementInventoryManagementTask = RunAuthorizedModuleAsync(
            "Procurement and Inventory Management",
            "Procurement",
            async sp => await sp.GetRequiredService<ProcurementInventoryManagementDashboardService>()
                .GetAsync(rangeStartDate, rangeEndDate, warehouseId, locationId),
            fallback: (ProcurementInventoryManagementDashboardDto?)null,
            fallbackMessage: "Procurement and inventory management metrics are unavailable");

        await Task.WhenAll(
            reportingCurrencyTask,
            financeTask,
            crmTask,
            projectDashboardTask,
            procurementQueuesTask,
            inventoryQueuesTask,
            maintenanceOverviewTask,
            maintenanceMetricsTask,
            maintenanceTrendsTask,
            procurementInventoryManagementTask);

        return new EnterpriseDashboardDto
        {
            ReportingCurrency = reportingCurrencyTask.Result.Data,
            FinanceOverview = financeTask.Result.Data,
            Crm = crmTask.Result.Data,
            ProjectDashboard = projectDashboardTask.Result.Data,
            OperationalQueues = MergeOperationalQueues(
                procurementQueuesTask.Result.Data,
                inventoryQueuesTask.Result.Data),
            MaintenanceOverview = maintenanceOverviewTask.Result.Data,
            MaintenanceMetrics = maintenanceMetricsTask.Result.Data,
            MaintenanceTrends = maintenanceTrendsTask.Result.Data,
            ProcurementInventoryManagement = procurementInventoryManagementTask.Result.Data,
            RangeStartDate = rangeStartDate,
            RangeEndDate = rangeEndDate,
            ModuleStatus =
            [
                reportingCurrencyTask.Result.Status,
                financeTask.Result.Status,
                crmTask.Result.Status,
                projectDashboardTask.Result.Status,
                procurementQueuesTask.Result.Status,
                inventoryQueuesTask.Result.Status,
                maintenanceOverviewTask.Result.Status,
                maintenanceMetricsTask.Result.Status,
                maintenanceTrendsTask.Result.Status,
                procurementInventoryManagementTask.Result.Status
            ],
            LastUpdated = DateTime.UtcNow
        };
    }

    private static DateTime SpecifyUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static EnterpriseOperationalQueueDto MergeOperationalQueues(
        EnterpriseOperationalQueueDto procurement,
        EnterpriseOperationalQueueDto inventory) => new()
    {
        PendingPurchaseRequisitionCount = procurement.PendingPurchaseRequisitionCount,
        OpenPurchaseOrderCount = procurement.OpenPurchaseOrderCount,
        PendingInventoryApprovalCount = inventory.PendingInventoryApprovalCount,
        PendingInventoryIssueCount = inventory.PendingInventoryIssueCount,
        OpenTenderCount = procurement.OpenTenderCount,
        TendersClosingWithin14DaysCount = procurement.TendersClosingWithin14DaysCount,
        OpenTendersByStatus = procurement.OpenTendersByStatus
    };

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
        catch (Exception ex) when (ex is InventoryAnalyticsAuthorizationException or ProcurementAccessAuthorizationException)
        {
            _logger.LogInformation("Enterprise dashboard module {Module} is outside the actor's assigned access", module);

            return new ModuleResult<T>(
                fallback,
                new EnterpriseDashboardModuleStatusDto
                {
                    Module = module,
                    Available = false,
                    AccessRestricted = true
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
                    Error = fallbackMessage
                });
        }
    }

    private async Task<ModuleResult<T>> RunAuthorizedModuleAsync<T>(
        string module,
        string permissionPolicy,
        Func<IServiceProvider, Task<T>> action,
        T fallback,
        string fallbackMessage)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var principal = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true ||
                !(await authorization.AuthorizeAsync(principal, resource: null, permissionPolicy)).Succeeded)
            {
                return new ModuleResult<T>(
                    fallback,
                    new EnterpriseDashboardModuleStatusDto
                    {
                        Module = module,
                        Available = false,
                        AccessRestricted = true
                    });
            }

            var data = await action(scope.ServiceProvider);
            return new ModuleResult<T>(
                data,
                new EnterpriseDashboardModuleStatusDto
                {
                    Module = module,
                    Available = true
                });
        }
        catch (Exception ex) when (ex is InventoryAnalyticsAuthorizationException or ProcurementAccessAuthorizationException)
        {
            _logger.LogInformation("Enterprise dashboard module {Module} is outside the actor's assigned access", module);
            return new ModuleResult<T>(
                fallback,
                new EnterpriseDashboardModuleStatusDto
                {
                    Module = module,
                    Available = false,
                    AccessRestricted = true
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
                    Error = fallbackMessage
                });
        }
    }

    private sealed record ModuleResult<T>(T Data, EnterpriseDashboardModuleStatusDto Status);
}
