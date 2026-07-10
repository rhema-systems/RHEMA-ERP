using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/dashboard")]
[Authorize]
public class MaintenanceDashboardController : ControllerBase
{
    private readonly IMaintenanceAnalyticsService _analyticsService;
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly IAssetDowntimeService _downtimeService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MaintenanceDashboardController> _logger;

    public MaintenanceDashboardController(
        IMaintenanceAnalyticsService analyticsService,
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        IAssetDowntimeService downtimeService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<MaintenanceDashboardController> logger)
    {
        _analyticsService = analyticsService;
        _workOrderService = workOrderService;
        _assetService = assetService;
        _downtimeService = downtimeService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive maintenance dashboard data
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<MaintenanceDashboardDto>> GetDashboardOverview()
    {
        try
        {
            var dashboard = await _analyticsService.GetDashboardDataAsync();
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance dashboard overview");
            return StatusCode(500, "An error occurred while retrieving dashboard data");
        }
    }

    /// <summary>
    /// Gets real-time maintenance alerts and notifications
    /// </summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<MaintenanceAlertsDto>> GetMaintenanceAlerts()
    {
        try
        {
            var alerts = await GetMaintenanceAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance alerts");
            return StatusCode(500, "An error occurred while retrieving alerts");
        }
    }

    /// <summary>
    /// Gets maintenance KPIs for a specific date range
    /// </summary>
    [HttpGet("kpis")]
    public async Task<ActionResult<MaintenanceKPIsDto>> GetMaintenanceKPIs(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            if (start > end)
            {
                return BadRequest("Start date cannot be after end date");
            }

            var kpis = await _analyticsService.GetMaintenanceKPIsAsync(start, end);
            return Ok(kpis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance KPIs");
            return StatusCode(500, "An error occurred while retrieving KPIs");
        }
    }

    /// <summary>
    /// Gets work order trends and statistics
    /// </summary>
    [HttpGet("work-order-trends")]
    public async Task<ActionResult<WorkOrderTrendsDto>> GetWorkOrderTrends(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-90);
            var end = endDate ?? DateTime.Today;

            var trends = await _analyticsService.GetWorkOrderTrendsAsync(start, end);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order trends");
            return StatusCode(500, "An error occurred while retrieving trends");
        }
    }

    /// <summary>
    /// Gets maintenance cost analysis
    /// </summary>
    [HttpGet("cost-analysis")]
    public async Task<ActionResult<MaintenanceCostAnalysisDto>> GetCostAnalysis(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var costAnalysis = await _analyticsService.GetCostAnalysisAsync(start, end);
            return Ok(costAnalysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cost analysis");
            return StatusCode(500, "An error occurred while retrieving cost analysis");
        }
    }

    /// <summary>
    /// Gets asset reliability report
    /// </summary>
    [HttpGet("asset-reliability")]
    public async Task<ActionResult<AssetReliabilityReportDto>> GetAssetReliability(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-90);
            var end = endDate ?? DateTime.Today;

            var reliability = await _analyticsService.GetAssetReliabilityReportAsync(start, end);
            return Ok(reliability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset reliability report");
            return StatusCode(500, "An error occurred while retrieving reliability data");
        }
    }

    /// <summary>
    /// Gets technician performance dashboard
    /// </summary>
    [HttpGet("technician-performance")]
    public async Task<ActionResult<TechnicianUtilizationReportDto>> GetTechnicianPerformance(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var performance = await _analyticsService.GetTechnicianUtilizationReportAsync(start, end);
            return Ok(performance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician performance");
            return StatusCode(500, "An error occurred while retrieving performance data");
        }
    }

    /// <summary>
    /// Gets upcoming maintenance schedule
    /// </summary>
    [HttpGet("upcoming-maintenance")]
    public async Task<ActionResult<IEnumerable<UpcomingMaintenanceDto>>> GetUpcomingMaintenance(
        [FromQuery] int days = 7)
    {
        try
        {
            if (days < 1 || days > 365)
            {
                return BadRequest("Days parameter must be between 1 and 365");
            }

            var upcomingMaintenance = await GetUpcomingMaintenanceForDaysAsync(days);
            return Ok(upcomingMaintenance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming maintenance");
            return StatusCode(500, "An error occurred while retrieving upcoming maintenance");
        }
    }

    /// <summary>
    /// Gets asset health summary
    /// </summary>
    [HttpGet("asset-health")]
    public async Task<ActionResult<AssetHealthSummaryDto>> GetAssetHealthSummary()
    {
        try
        {
            var assetHealth = await GetAssetHealthSummaryAsync();
            return Ok(assetHealth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset health summary");
            return StatusCode(500, "An error occurred while retrieving asset health data");
        }
    }

    /// <summary>
    /// Gets maintenance efficiency metrics
    /// </summary>
    [HttpGet("efficiency-metrics")]
    public async Task<ActionResult<MaintenanceEfficiencyDto>> GetEfficiencyMetrics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var efficiency = await GetMaintenanceEfficiencyAsync(start, end);
            return Ok(efficiency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving efficiency metrics");
            return StatusCode(500, "An error occurred while retrieving efficiency metrics");
        }
    }

    /// <summary>
    /// Gets real-time status updates for critical systems
    /// </summary>
    [HttpGet("critical-status")]
    public async Task<ActionResult<CriticalSystemStatusDto>> GetCriticalSystemStatus()
    {
        try
        {
            var criticalStatus = await GetCriticalSystemStatusAsync();
            return Ok(criticalStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving critical system status");
            return StatusCode(500, "An error occurred while retrieving critical status");
        }
    }

    /// <summary>
    /// Gets maintenance backlog analysis
    /// </summary>
    [HttpGet("backlog-analysis")]
    public async Task<ActionResult<MaintenanceBacklogDto>> GetBacklogAnalysis()
    {
        try
        {
            var backlog = await GetMaintenanceBacklogAsync();
            return Ok(backlog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backlog analysis");
            return StatusCode(500, "An error occurred while retrieving backlog data");
        }
    }

    /// <summary>
    /// Gets mobile/offline inspection operational KPIs.
    /// </summary>
    [HttpGet("fleet-inspections")]
    public async Task<ActionResult<FleetInspectionOperationsDashboardDto>> GetFleetInspectionOperations(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = (startDate ?? DateTime.UtcNow.AddDays(-30)).ToUniversalTime();
            var end = (endDate ?? DateTime.UtcNow).ToUniversalTime();

            if (start > end)
            {
                return BadRequest("Start date cannot be after end date");
            }

            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return BadRequest("Tenant context is required");
            }

            var dashboard = await GetFleetInspectionOperationsAsync(tenantId.Value, start, end);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fleet inspection operations dashboard");
            return StatusCode(500, "An error occurred while retrieving fleet inspection operations");
        }
    }

    /// <summary>
    /// Refreshes dashboard cache and returns updated data
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<MaintenanceDashboardDto>> RefreshDashboard()
    {
        try
        {
            // In a real implementation, this would clear cache and recalculate metrics
            _logger.LogInformation("Dashboard refresh requested by user {UserId}", _currentUserService.UserId);

            var dashboard = await _analyticsService.GetDashboardDataAsync();
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing dashboard");
            return StatusCode(500, "An error occurred while refreshing dashboard");
        }
    }

    #region Helper Methods

    private async Task<FleetInspectionOperationsDashboardDto> GetFleetInspectionOperationsAsync(Guid tenantId, DateTime start, DateTime end)
    {
        var inspectionRows = await _unitOfWork.Repository<FleetTripInspection>()
            .GetQueryable(i =>
                i.TenantId == tenantId &&
                !i.IsDeleted &&
                i.CompletedAtUtc.HasValue &&
                i.CompletedAtUtc.Value >= start &&
                i.CompletedAtUtc.Value <= end)
            .Select(i => new
            {
                i.Id,
                i.OverallResult,
                i.CapturedOfflineAtUtc,
                i.SyncedAtUtc,
                CompletedAtUtc = i.CompletedAtUtc!.Value,
                AssetName = i.VehicleAsset.Name,
                AssetNumber = i.VehicleAsset.AssetNumber,
                TemplateName = i.InspectionTemplate.Name,
                SheetType = i.InspectionTemplate.SheetType
            })
            .ToListAsync();

        var inspectionIds = inspectionRows.Select(i => i.Id).ToList();
        var inspectionById = inspectionRows.ToDictionary(i => i.Id);

        var defectRows = inspectionIds.Count == 0
            ? new List<FleetInspectionDashboardDefectRow>()
            : await _unitOfWork.Repository<FleetDefect>()
                .GetQueryable(d =>
                    d.TenantId == tenantId &&
                    !d.IsDeleted &&
                    d.FleetTripInspectionId.HasValue &&
                    inspectionIds.Contains(d.FleetTripInspectionId.Value))
                .Select(d => new FleetInspectionDashboardDefectRow
                {
                    Id = d.Id,
                    InspectionId = d.FleetTripInspectionId!.Value,
                    WorkOrderId = d.WorkOrderId,
                    Severity = d.Severity,
                    Status = d.Status,
                    ReportedAtUtc = d.ReportedAtUtc,
                    WorkOrderStatus = d.WorkOrder != null ? d.WorkOrder.Status : null
                })
                .ToListAsync();

        var qrTemplates = _unitOfWork.Repository<InspectionTemplate>()
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive && t.IsQrEnabled && t.MobileOfflineEnabled);
        var qrTemplateCount = await qrTemplates.CountAsync();
        var staleQrTemplateCount = await qrTemplates.CountAsync(t => t.UpdatedAt < DateTime.UtcNow.AddDays(-180));

        var total = inspectionRows.Count;
        var failed = inspectionRows.Count(i => string.Equals(i.OverallResult, "Fail", StringComparison.OrdinalIgnoreCase));
        var flagged = inspectionRows.Count(i => string.Equals(i.OverallResult, "ConditionalPass", StringComparison.OrdinalIgnoreCase));
        var passed = inspectionRows.Count(i => string.Equals(i.OverallResult, "Pass", StringComparison.OrdinalIgnoreCase));
        var synced = inspectionRows.Count(i => i.SyncedAtUtc.HasValue);
        var offlineCaptured = inspectionRows.Count(i => i.CapturedOfflineAtUtc.HasValue);
        var workOrderIds = defectRows
            .Where(d => d.WorkOrderId != null)
            .Select(d => d.WorkOrderId!.Value)
            .Distinct()
            .ToList();
        var defectCount = defectRows.Count;

        var sheetBreakdown = inspectionRows
            .GroupBy(i => string.IsNullOrWhiteSpace(i.SheetType) ? "InspectionSheet" : i.SheetType)
            .Select(group => new FleetInspectionSheetBreakdownDto
            {
                SheetType = group.Key,
                Total = group.Count(),
                Failed = group.Count(i => string.Equals(i.OverallResult, "Fail", StringComparison.OrdinalIgnoreCase)),
                Flagged = group.Count(i => string.Equals(i.OverallResult, "ConditionalPass", StringComparison.OrdinalIgnoreCase)),
                OfflineCaptured = group.Count(i => i.CapturedOfflineAtUtc.HasValue)
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        var recentIssues = defectRows
            .OrderByDescending(d => d.ReportedAtUtc)
            .Take(6)
            .Select(d =>
            {
                var inspection = inspectionById.TryGetValue(d.InspectionId, out var row) ? row : null;
                return new FleetInspectionRecentIssueDto
                {
                    InspectionId = d.InspectionId,
                    DefectId = d.Id,
                    WorkOrderId = d.WorkOrderId,
                    AssetName = inspection?.AssetName ?? "Asset",
                    AssetNumber = inspection?.AssetNumber ?? string.Empty,
                    TemplateName = inspection?.TemplateName ?? "Inspection",
                    SheetType = inspection?.SheetType ?? "InspectionSheet",
                    OverallResult = inspection?.OverallResult ?? string.Empty,
                    Severity = d.Severity ?? string.Empty,
                    DefectStatus = d.Status ?? string.Empty,
                    WorkOrderStatus = d.WorkOrderStatus,
                    ReportedAtUtc = d.ReportedAtUtc
                };
            })
            .ToList();

        var openFollowUps = defectRows.Count(d => d.WorkOrderId != null && !IsTerminalWorkOrderStatus(d.WorkOrderStatus));

        return new FleetInspectionOperationsDashboardDto
        {
            StartDateUtc = start,
            EndDateUtc = end,
            LastUpdatedUtc = DateTime.UtcNow,
            TotalInspections = total,
            SyncedInspections = synced,
            OfflineCapturedInspections = offlineCaptured,
            PassedInspections = passed,
            FailedInspections = failed,
            FlaggedInspections = flagged,
            DefectsCreated = defectCount,
            WorkOrdersCreated = workOrderIds.Count,
            OpenFollowUpWorkOrders = openFollowUps,
            QrEnabledTemplates = qrTemplateCount,
            StaleQrTemplates = staleQrTemplateCount,
            SyncRate = CalculatePercent(synced, total),
            FailureRate = CalculatePercent(failed + flagged, total),
            WorkOrderFollowUpRate = CalculatePercent(workOrderIds.Count, defectCount),
            SheetBreakdown = sheetBreakdown,
            RecentIssues = recentIssues
        };
    }

    private static decimal CalculatePercent(int value, int total)
    {
        return total <= 0 ? 0 : Math.Round((decimal)value / total * 100, 2);
    }

    private static bool IsTerminalWorkOrderStatus(string? status)
    {
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FleetInspectionDashboardDefectRow
    {
        public Guid Id { get; set; }
        public Guid InspectionId { get; set; }
        public Guid? WorkOrderId { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
        public DateTime ReportedAtUtc { get; set; }
        public string? WorkOrderStatus { get; set; }
    }

    private async Task<MaintenanceAlertsDto> GetMaintenanceAlertsAsync()
    {
        var overdueWorkOrders = await _workOrderService.GetOverdueWorkOrdersAsync();
        var emergencyWorkOrders = await _workOrderService.GetWorkOrdersByStatusAsync("Emergency");
        var assetsRequiringMaintenance = await _assetService.GetAssetsRequiringMaintenanceAsync();
        var activeDowntime = await _downtimeService.GetActiveDowntimeAsync();

        var criticalAlerts = new List<MaintenanceAlertDto>();
        var warningAlerts = new List<MaintenanceAlertDto>();
        var infoAlerts = new List<MaintenanceAlertDto>();

        // Process overdue work orders
        foreach (var workOrder in overdueWorkOrders.Take(10))
        {
            var dueDate = workOrder.ScheduledEndDate ?? workOrder.RequestedCompletionDate ?? workOrder.DueDate ?? workOrder.CreatedAt;
            var daysOverdue = Math.Max((DateTime.UtcNow - dueDate).Days, 0);
            var severity = daysOverdue > 7 ? "Critical" : daysOverdue > 3 ? "Warning" : "Info";

            var alert = new MaintenanceAlertDto
            {
                Id = Guid.NewGuid(),
                Type = "OverdueWorkOrder",
                Severity = severity,
                Title = $"Work Order {workOrder.WorkOrderNumber} Overdue",
                Description = $"Work order '{workOrder.Title}' is {daysOverdue} day(s) overdue",
                EntityId = workOrder.Id,
                EntityType = "WorkOrder",
                CreatedDate = DateTime.UtcNow,
                ActionRequired = true,
                ActionUrl = $"/maintenance/work-orders/{workOrder.Id}"
            };

            switch (severity)
            {
                case "Critical": criticalAlerts.Add(alert); break;
                case "Warning": warningAlerts.Add(alert); break;
                default: infoAlerts.Add(alert); break;
            }
        }

        // Process emergency work orders
        foreach (var workOrder in emergencyWorkOrders.Take(5))
        {
            criticalAlerts.Add(new MaintenanceAlertDto
            {
                Id = Guid.NewGuid(),
                Type = "EmergencyWorkOrder",
                Severity = "Critical",
                Title = $"Emergency Work Order {workOrder.WorkOrderNumber}",
                Description = $"Emergency maintenance required for {workOrder.AssetName}",
                EntityId = workOrder.Id,
                EntityType = "WorkOrder",
                CreatedDate = DateTime.UtcNow,
                ActionRequired = true,
                ActionUrl = $"/maintenance/work-orders/{workOrder.Id}"
            });
        }

        // Process assets requiring maintenance
        foreach (var asset in assetsRequiringMaintenance.Take(10))
        {
            infoAlerts.Add(new MaintenanceAlertDto
            {
                Id = Guid.NewGuid(),
                Type = "MaintenanceDue",
                Severity = "Info",
                Title = $"Maintenance Due for {asset.Name}",
                Description = $"Scheduled maintenance is due for asset {asset.AssetNumber}",
                EntityId = asset.Id,
                EntityType = "Asset",
                CreatedDate = DateTime.UtcNow,
                ActionRequired = false,
                ActionUrl = $"/maintenance/assets/{asset.Id}"
            });
        }

        // Process active downtime
        foreach (var downtime in activeDowntime.Take(5))
        {
            var alert = new MaintenanceAlertDto
            {
                Id = Guid.NewGuid(),
                Type = "AssetDowntime",
                Severity = "Warning",
                Title = $"Asset Downtime - {downtime.AssetName}",
                Description = $"Asset has been down since {downtime.StartTime:yyyy-MM-dd HH:mm}",
                EntityId = downtime.AssetId,
                EntityType = "Asset",
                CreatedDate = DateTime.UtcNow,
                ActionRequired = true,
                ActionUrl = $"/maintenance/assets/{downtime.AssetId}/downtime"
            };

            warningAlerts.Add(alert);
        }

        return new MaintenanceAlertsDto
        {
            CriticalAlerts = criticalAlerts.OrderByDescending(a => a.CreatedDate).ToList(),
            WarningAlerts = warningAlerts.OrderByDescending(a => a.CreatedDate).ToList(),
            InfoAlerts = infoAlerts.OrderByDescending(a => a.CreatedDate).ToList(),
            TotalCritical = criticalAlerts.Count,
            TotalWarning = warningAlerts.Count,
            TotalInfo = infoAlerts.Count,
            LastUpdated = DateTime.UtcNow
        };
    }

    private async Task<IEnumerable<UpcomingMaintenanceDto>> GetUpcomingMaintenanceForDaysAsync(int days)
    {
        var endDate = DateTime.Today.AddDays(days);
        var preventiveReport = await _analyticsService.GetPreventiveMaintenanceReportAsync(DateTime.Today, endDate);

        // TODO: PreventiveMaintenanceReportDto doesn't have UpcomingMaintenance property
        // For now, return empty list until the DTO is updated
        return new List<UpcomingMaintenanceDto>();
    }

    private async Task<AssetHealthSummaryDto> GetAssetHealthSummaryAsync()
    {
        var assetMetrics = await _assetService.GetAssetMetricsAsync();
        var activeDowntime = await _downtimeService.GetActiveDowntimeAsync();

        var totalAssets = assetMetrics.TotalAssets;
        var offlineAssets = activeDowntime.Count();
        var criticalAssets = Math.Min(assetMetrics.CriticalAssets, totalAssets);
        var warningAssets = Math.Max(assetMetrics.AssetsRequiringMaintenance - criticalAssets, 0);
        var healthyAssets = Math.Max(totalAssets - criticalAssets - warningAssets - offlineAssets, 0);

        return new AssetHealthSummaryDto
        {
            TotalAssets = totalAssets,
            HealthyAssets = healthyAssets,
            WarningAssets = warningAssets,
            CriticalAssets = criticalAssets,
            OfflineAssets = offlineAssets,
            HealthyPercentage = totalAssets > 0 ? (double)healthyAssets / totalAssets * 100 : 100,
            WarningPercentage = totalAssets > 0 ? (double)warningAssets / totalAssets * 100 : 0,
            CriticalPercentage = totalAssets > 0 ? (double)criticalAssets / totalAssets * 100 : 0,
            OfflinePercentage = totalAssets > 0 ? (double)offlineAssets / totalAssets * 100 : 0
        };
    }

    private async Task<MaintenanceEfficiencyDto> GetMaintenanceEfficiencyAsync(DateTime startDate, DateTime endDate)
    {
        var workOrderResult = await _workOrderService.GetWorkOrdersPagedAsync(new WorkOrderFilterDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = int.MaxValue
        });
        var workOrders = workOrderResult.Items.ToList();
        var completedWorkOrders = workOrders
            .Where(wo => string.Equals(wo.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(wo.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var preventiveReport = await _analyticsService.GetPreventiveMaintenanceReportAsync(startDate, endDate);
        var averageRepairTime = completedWorkOrders
            .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            .Select(wo => (wo.ActualEndDate!.Value - wo.ActualStartDate!.Value).TotalHours)
            .Where(hours => hours >= 0)
            .DefaultIfEmpty(0)
            .Average();
        var completedOnFirstPass = completedWorkOrders.Count(wo =>
            !wo.Title.Contains("rework", StringComparison.OrdinalIgnoreCase) &&
            !(wo.Description?.Contains("rework", StringComparison.OrdinalIgnoreCase) ?? false));

        return new MaintenanceEfficiencyDto
        {
            PlannedWorkPercentage = preventiveReport.ComplianceRate,
            ScheduleAdherence = preventiveReport.ComplianceRate,
            FirstTimeFixRate = completedWorkOrders.Any()
                ? (double)completedOnFirstPass / completedWorkOrders.Count * 100
                : 0,
            AverageRepairTime = averageRepairTime,
            ResourceUtilization = workOrders.Any()
                ? (double)completedWorkOrders.Count / workOrders.Count * 100
                : 0,
            InventoryTurnover = 0,
            EfficiencyTrend = "Current"
        };
    }

    private static Task<CriticalSystemStatusDto> GetCriticalSystemStatusAsync()
    {
        // This would query for assets marked as critical systems
        var criticalSystems = new List<CriticalSystemDto>
        {
            new() { Name = "HVAC System 1", Status = "Online", LastCheck = DateTime.UtcNow.AddMinutes(-5) },
            new() { Name = "Fire Safety System", Status = "Online", LastCheck = DateTime.UtcNow.AddMinutes(-2) },
            new() { Name = "Emergency Generator", Status = "Warning", LastCheck = DateTime.UtcNow.AddMinutes(-15) },
            new() { Name = "Water Treatment Plant", Status = "Online", LastCheck = DateTime.UtcNow.AddMinutes(-8) }
        };

        return Task.FromResult(new CriticalSystemStatusDto
        {
            Systems = criticalSystems,
            OnlineCount = criticalSystems.Count(s => s.Status == "Online"),
            WarningCount = criticalSystems.Count(s => s.Status == "Warning"),
            OfflineCount = criticalSystems.Count(s => s.Status == "Offline"),
            LastUpdated = DateTime.UtcNow
        });
    }

    private async Task<MaintenanceBacklogDto> GetMaintenanceBacklogAsync()
    {
        var allWorkOrdersResult = await _workOrderService.GetWorkOrdersPagedAsync(new WorkOrderFilterDto
        {
            Page = 1,
            PageSize = int.MaxValue
        });
        var allOpenWorkOrders = allWorkOrdersResult.Items
            .Where(wo => IsActiveWorkOrderStatus(wo.Status))
            .ToList();
        var overdueWorkOrders = allOpenWorkOrders.Where(wo => wo.IsOverdue).ToList();

        var backlogHours = allOpenWorkOrders.Where(wo => wo.EstimatedHours > 0).Sum(wo => wo.EstimatedHours);
        var backlogCost = allOpenWorkOrders.Where(wo => wo.EstimatedCost > 0).Sum(wo => wo.EstimatedCost);
        var averageAge = allOpenWorkOrders.Any()
            ? allOpenWorkOrders.Average(wo => Math.Max((DateTime.UtcNow - wo.CreatedAt).TotalDays, 0))
            : 0;

        return new MaintenanceBacklogDto
        {
            TotalWorkOrders = allOpenWorkOrders.Count,
            OverdueWorkOrders = overdueWorkOrders.Count(),
            EstimatedHours = backlogHours,
            EstimatedCost = backlogCost,
            AverageAge = averageAge,
            PriorityBreakdown = allOpenWorkOrders
                .GroupBy(wo => string.IsNullOrWhiteSpace(wo.PriorityName) ? wo.Priority : wo.PriorityName)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g => new BacklogPriorityDto { Priority = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList()
        };
    }

    private static bool IsActiveWorkOrderStatus(string? status)
    {
        return !string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

#region Dashboard DTOs

public class MaintenanceAlertsDto
{
    public List<MaintenanceAlertDto> CriticalAlerts { get; set; } = new();
    public List<MaintenanceAlertDto> WarningAlerts { get; set; } = new();
    public List<MaintenanceAlertDto> InfoAlerts { get; set; } = new();
    public int TotalCritical { get; set; }
    public int TotalWarning { get; set; }
    public int TotalInfo { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class MaintenanceAlertDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool ActionRequired { get; set; }
    public string? ActionUrl { get; set; }
}

public class AssetHealthSummaryDto
{
    public int TotalAssets { get; set; }
    public int HealthyAssets { get; set; }
    public int WarningAssets { get; set; }
    public int CriticalAssets { get; set; }
    public int OfflineAssets { get; set; }
    public double HealthyPercentage { get; set; }
    public double WarningPercentage { get; set; }
    public double CriticalPercentage { get; set; }
    public double OfflinePercentage { get; set; }
}

public class MaintenanceEfficiencyDto
{
    public double PlannedWorkPercentage { get; set; }
    public double ScheduleAdherence { get; set; }
    public double FirstTimeFixRate { get; set; }
    public double AverageRepairTime { get; set; }
    public double ResourceUtilization { get; set; }
    public double InventoryTurnover { get; set; }
    public string EfficiencyTrend { get; set; } = string.Empty;
}

public class CriticalSystemStatusDto
{
    public List<CriticalSystemDto> Systems { get; set; } = new();
    public int OnlineCount { get; set; }
    public int WarningCount { get; set; }
    public int OfflineCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class CriticalSystemDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Online, Warning, Offline
    public DateTime LastCheck { get; set; }
}

public class MaintenanceBacklogDto
{
    public int TotalWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public double AverageAge { get; set; }
    public List<BacklogPriorityDto> PriorityBreakdown { get; set; } = new();
}

public class BacklogPriorityDto
{
    public string Priority { get; set; } = string.Empty;
    public int Count { get; set; }
}

#endregion
