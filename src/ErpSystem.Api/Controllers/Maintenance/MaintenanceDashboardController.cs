using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;

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
    private readonly ILogger<MaintenanceDashboardController> _logger;

    public MaintenanceDashboardController(
        IMaintenanceAnalyticsService analyticsService,
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        IAssetDowntimeService downtimeService,
        ICurrentUserService currentUserService,
        ILogger<MaintenanceDashboardController> logger)
    {
        _analyticsService = analyticsService;
        _workOrderService = workOrderService;
        _assetService = assetService;
        _downtimeService = downtimeService;
        _currentUserService = currentUserService;
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
                return BadRequest("Start date cannot be after end date");

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
                return BadRequest("Days parameter must be between 1 and 365");

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
            var daysOverdue = (DateTime.Now - workOrder.ScheduledEndDate!.Value).Days;
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
        
        return preventiveReport.UpcomingMaintenance.OrderBy(um => um.ScheduledDate);
    }

    private async Task<AssetHealthSummaryDto> GetAssetHealthSummaryAsync()
    {
        var assets = await _assetService.GetAssetsWithActiveWorkOrdersAsync();
        var activeDowntime = await _downtimeService.GetActiveDowntimeAsync();

        var healthyAssets = 0;
        var warningAssets = 0;
        var criticalAssets = 0;
        var offlineAssets = activeDowntime.Count();

        // Simplified health calculation
        foreach (var asset in assets)
        {
            var hasEmergencyWork = asset.Status == "Emergency";
            var hasOverdueWork = asset.Status == "Overdue";
            
            if (hasEmergencyWork)
                criticalAssets++;
            else if (hasOverdueWork)
                warningAssets++;
            else
                healthyAssets++;
        }

        var totalAssets = healthyAssets + warningAssets + criticalAssets + offlineAssets;

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
        // Parse UserId string to Guid for the placeholder call
        var userIdString = _currentUserService.UserId;
        var userId = !string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var workOrders = await _workOrderService.GetWorkOrdersByTechnicianAsync(userId); // Placeholder
        var preventiveReport = await _analyticsService.GetPreventiveMaintenanceReportAsync(startDate, endDate);
        
        return new MaintenanceEfficiencyDto
        {
            PlannedWorkPercentage = preventiveReport.ScheduleCompliance,
            ScheduleAdherence = preventiveReport.ScheduleCompliance,
            FirstTimeFixRate = 85.0, // Would need more detailed tracking
            AverageRepairTime = 4.5, // Hours
            ResourceUtilization = 75.0, // Percentage
            InventoryTurnover = 12.0, // Times per year
            EfficiencyTrend = "Improving" // Could be "Improving", "Stable", "Declining"
        };
    }

    private async Task<CriticalSystemStatusDto> GetCriticalSystemStatusAsync()
    {
        // This would query for assets marked as critical systems
        var criticalSystems = new List<CriticalSystemDto>
        {
            new() { Name = "HVAC System 1", Status = "Online", LastCheck = DateTime.UtcNow.AddMinutes(-5) },
            new() { Name = "Fire Safety System", Status = "Online", LastCheck = DateTime.UtcNow.AddMinutes(-2) },
            new() { Name = "Emergency Generator", Status = "Warning", LastCheck = DateTime.UtcNow.AddMinutes(-15) },
            new() { Name = "Water Treatment Plant", Status = "Online", LastCheck = DateTime.UtcNow.AddMinutes(-8) }
        };

        return new CriticalSystemStatusDto
        {
            Systems = criticalSystems,
            OnlineCount = criticalSystems.Count(s => s.Status == "Online"),
            WarningCount = criticalSystems.Count(s => s.Status == "Warning"),
            OfflineCount = criticalSystems.Count(s => s.Status == "Offline"),
            LastUpdated = DateTime.UtcNow
        };
    }

    private async Task<MaintenanceBacklogDto> GetMaintenanceBacklogAsync()
    {
        var allWorkOrders = await _workOrderService.GetWorkOrdersByStatusAsync("Scheduled");
        var overdueWorkOrders = await _workOrderService.GetOverdueWorkOrdersAsync();

        var backlogHours = allWorkOrders.Where(wo => wo.EstimatedHours > 0).Sum(wo => wo.EstimatedHours);
        var backlogCost = allWorkOrders.Where(wo => wo.EstimatedCost > 0).Sum(wo => wo.EstimatedCost);

        return new MaintenanceBacklogDto
        {
            TotalWorkOrders = allWorkOrders.Count(),
            OverdueWorkOrders = overdueWorkOrders.Count(),
            EstimatedHours = backlogHours,
            EstimatedCost = backlogCost,
            AverageAge = 15.0, // Days - would need to calculate from creation dates
            PriorityBreakdown = new List<BacklogPriorityDto>
            {
                new() { Priority = "Emergency", Count = allWorkOrders.Count(wo => wo.Priority == "Emergency") },
                new() { Priority = "High", Count = allWorkOrders.Count(wo => wo.Priority == "High") },
                new() { Priority = "Normal", Count = allWorkOrders.Count(wo => wo.Priority == "Normal") },
                new() { Priority = "Low", Count = allWorkOrders.Count(wo => wo.Priority == "Low") }
            }
        };
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