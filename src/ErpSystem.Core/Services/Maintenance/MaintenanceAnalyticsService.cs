using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for advanced maintenance analytics and KPI calculations
/// </summary>
public class MaintenanceAnalyticsService : IMaintenanceAnalyticsService
{
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly ITechnicianService _technicianService;
    private readonly ISafetyProtocolService _safetyProtocolService;
    private readonly ILogger<MaintenanceAnalyticsService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public MaintenanceAnalyticsService(
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        IMaintenanceScheduleService scheduleService,
        ITechnicianService technicianService,
        ISafetyProtocolService safetyProtocolService,
        ILogger<MaintenanceAnalyticsService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _workOrderService = workOrderService;
        _assetService = assetService;
        _scheduleService = scheduleService;
        _technicianService = technicianService;
        _safetyProtocolService = safetyProtocolService;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    #region Dashboard Analytics

    public async Task<MaintenanceDashboardDto> GetDashboardDataAsync()
    {
        try
        {
            _logger.LogInformation("Generating maintenance dashboard data");

            var tasks = new List<Task>
            {
                GetAssetMetricsAsync(),
                GetWorkOrderMetricsAsync(),
                GetScheduleMetricsAsync(),
                GetTechnicianMetricsAsync(),
                GetSafetyMetricsAsync()
            };

            await Task.WhenAll(tasks);

            var assetMetrics = await (tasks[0] as Task<AssetMetricsDto>);
            var workOrderMetrics = await (tasks[1] as Task<WorkOrderMetricsDto>);
            var scheduleMetrics = await (tasks[2] as Task<ScheduleMetricsDto>);
            var technicianMetrics = await (tasks[3] as Task<TechnicianMetricsDto>);
            var safetyMetrics = await (tasks[4] as Task<SafetyMetricsDto>);

            var dashboard = new MaintenanceDashboardDto
            {
                Summary = new DashboardSummaryDto
                {
                    TotalAssets = assetMetrics?.TotalAssets ?? 0,
                    ActiveAssets = assetMetrics?.ActiveAssets ?? 0,
                    CriticalAssets = assetMetrics?.CriticalAssets ?? 0,
                    AssetsRequiringMaintenance = assetMetrics?.AssetsRequiringMaintenance ?? 0,
                    TotalWorkOrders = workOrderMetrics?.TotalWorkOrders ?? 0,
                    ActiveWorkOrders = workOrderMetrics?.ActiveWorkOrders ?? 0,
                    OverdueWorkOrders = workOrderMetrics?.OverdueWorkOrders ?? 0,
                    CompletedWorkOrders = workOrderMetrics?.CompletedWorkOrders ?? 0,
                    TotalTechnicians = technicianMetrics?.TotalTechnicians ?? 0,
                    AvailableTechnicians = technicianMetrics?.AvailableTechnicians ?? 0,
                    ActiveProtocols = safetyMetrics?.ActiveProtocols ?? 0,
                    ComplianceRate = safetyMetrics?.OverallComplianceRate ?? 0
                },
                
                WorkOrdersByStatus = workOrderMetrics?.WorkOrdersByStatus ?? new Dictionary<string, int>(),
                
                WorkOrdersByPriority = workOrderMetrics?.WorkOrdersByPriority ?? new Dictionary<string, int>(),
                
                AssetsByStatus = assetMetrics?.AssetsByStatus ?? new Dictionary<string, int>(),
                
                MaintenanceKPIs = new MaintenanceKPIsDto
                {
                    MTTR = workOrderMetrics?.MTTR ?? 0,
                    MTBF = assetMetrics?.MTBF ?? 0,
                    ScheduleCompliance = scheduleMetrics?.ComplianceRate ?? 0,
                    FirstTimeFixRate = workOrderMetrics?.FirstTimeFixRate ?? 0,
                    PlannedMaintenancePercentage = scheduleMetrics?.PlannedMaintenancePercentage ?? 0,
                    OverallEquipmentEffectiveness = assetMetrics?.OEE ?? 0
                },

                UpcomingMaintenance = await GetUpcomingMaintenanceAsync(7),
                
                RecentAlerts = await GetRecentAlertsAsync(10),
                
                TopIssues = await GetTopIssuesAsync(5)
            };

            _logger.LogInformation("Dashboard data generated successfully");
            return dashboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dashboard data");
            throw;
        }
    }

    public async Task<MaintenanceKPIsDto> GetMaintenanceKPIsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Calculating maintenance KPIs for period {StartDate} to {EndDate}", startDate, endDate);

            // Get work orders for the period
            var workOrders = await _workOrderService.GetWorkOrdersPagedAsync(new WorkOrderFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                Page = 1,
                PageSize = int.MaxValue
            });

            var completedWorkOrders = workOrders.Data.Where(wo => wo.Status == "Completed").ToList();
            var totalWorkOrders = workOrders.Data.Count();

            // Calculate MTTR (Mean Time To Repair)
            var mttr = CalculateMTTR(completedWorkOrders);

            // Calculate First Time Fix Rate
            var firstTimeFixRate = CalculateFirstTimeFixRate(completedWorkOrders);

            // Calculate Schedule Compliance
            var scheduleCompliance = await CalculateScheduleComplianceAsync(startDate, endDate);

            // Calculate Planned Maintenance Percentage
            var plannedMaintenancePercentage = CalculatePlannedMaintenancePercentage(workOrders.Data);

            return new MaintenanceKPIsDto
            {
                MTTR = mttr,
                MTBF = await CalculateMTBFAsync(startDate, endDate),
                ScheduleCompliance = scheduleCompliance,
                FirstTimeFixRate = firstTimeFixRate,
                PlannedMaintenancePercentage = plannedMaintenancePercentage,
                OverallEquipmentEffectiveness = await CalculateOEEAsync(startDate, endDate),
                MaintenanceCostPerAsset = await CalculateMaintenanceCostPerAssetAsync(startDate, endDate),
                WorkOrderCompletionRate = totalWorkOrders > 0 ? (completedWorkOrders.Count / (double)totalWorkOrders * 100) : 0,
                AverageWorkOrderDuration = CalculateAverageWorkOrderDuration(completedWorkOrders),
                PreventiveMaintenanceRatio = await CalculatePreventiveMaintenanceRatioAsync(startDate, endDate)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating maintenance KPIs");
            throw;
        }
    }

    #endregion

    #region Asset Performance Analytics

    public async Task<AssetPerformanceReportDto> GetAssetPerformanceReportAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Generating asset performance report for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {assetId} not found");

            var workOrders = await _workOrderService.GetWorkOrdersByAssetAsync(assetId);
            var periodWorkOrders = workOrders.Where(wo => wo.ScheduledStartDate >= startDate && wo.ScheduledStartDate <= endDate).ToList();

            return new AssetPerformanceReportDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                AssetNumber = asset.AssetNumber,
                ReportPeriod = new DatePeriodDto { StartDate = startDate, EndDate = endDate },
                
                TotalWorkOrders = periodWorkOrders.Count,
                CompletedWorkOrders = periodWorkOrders.Count(wo => wo.Status == "Completed"),
                PlannedWorkOrders = periodWorkOrders.Count(wo => wo.WorkOrderSource == "Scheduled"),
                UnplannedWorkOrders = periodWorkOrders.Count(wo => wo.WorkOrderSource != "Scheduled"),
                
                TotalDowntime = CalculateTotalDowntime(periodWorkOrders),
                AverageRepairTime = CalculateAverageRepairTime(periodWorkOrders),
                
                MaintenanceCost = periodWorkOrders.Sum(wo => wo.ActualCost ?? 0),
                LaborHours = periodWorkOrders.Sum(wo => wo.ActualHours ?? 0),
                
                Availability = CalculateAssetAvailability(asset, periodWorkOrders, startDate, endDate),
                Reliability = CalculateAssetReliability(periodWorkOrders, startDate, endDate),
                
                MaintenanceHistory = periodWorkOrders.OrderByDescending(wo => wo.CreatedAt).Take(10).ToList(),
                
                Recommendations = GenerateAssetRecommendations(asset, periodWorkOrders)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating asset performance report for asset {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Cost Analysis

    public async Task<MaintenanceCostAnalysisDto> GetCostAnalysisAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Generating maintenance cost analysis for period {StartDate} to {EndDate}", startDate, endDate);

            var workOrders = await _workOrderService.GetWorkOrdersPagedAsync(new WorkOrderFilterDto
            {
                StartDate = startDate,
                EndDate = endDate,
                Page = 1,
                PageSize = int.MaxValue
            });

            var completedWorkOrders = workOrders.Data.Where(wo => wo.Status == "Completed").ToList();

            return new MaintenanceCostAnalysisDto
            {
                ReportPeriod = new DatePeriodDto { StartDate = startDate, EndDate = endDate },
                
                TotalMaintenanceCost = completedWorkOrders.Sum(wo => wo.ActualCost ?? 0),
                LaborCost = completedWorkOrders.Sum(wo => CalculateLaborCost(wo)),
                PartsCost = completedWorkOrders.Sum(wo => CalculatePartsCost(wo)),
                ContractorCost = completedWorkOrders.Sum(wo => CalculateContractorCost(wo)),
                
                PlannedMaintenanceCost = completedWorkOrders
                    .Where(wo => wo.WorkOrderSource == "Scheduled")
                    .Sum(wo => wo.ActualCost ?? 0),
                
                UnplannedMaintenanceCost = completedWorkOrders
                    .Where(wo => wo.WorkOrderSource != "Scheduled")
                    .Sum(wo => wo.ActualCost ?? 0),
                
                CostByCategory = GroupCostByCategory(completedWorkOrders),
                CostByAsset = GroupCostByAsset(completedWorkOrders),
                CostTrend = CalculateCostTrend(completedWorkOrders, startDate, endDate),
                
                BudgetVariance = await CalculateBudgetVarianceAsync(startDate, endDate),
                CostPerWorkOrder = completedWorkOrders.Any() ? 
                    completedWorkOrders.Sum(wo => wo.ActualCost ?? 0) / completedWorkOrders.Count : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating cost analysis");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private async Task<AssetMetricsDto> GetAssetMetricsAsync()
    {
        var assetMetrics = await _assetService.GetAssetMetricsAsync();
        return new AssetMetricsDto
        {
            TotalAssets = assetMetrics?.TotalAssets ?? 0,
            ActiveAssets = assetMetrics?.ActiveAssets ?? 0,
            CriticalAssets = assetMetrics?.CriticalAssets ?? 0,
            AssetsRequiringMaintenance = assetMetrics?.AssetsRequiringMaintenance ?? 0,
            AssetsByStatus = assetMetrics?.AssetsByStatus ?? new Dictionary<string, int>(),
            MTBF = await CalculateMTBFAsync(DateTime.Now.AddMonths(-3), DateTime.Now),
            OEE = await CalculateOEEAsync(DateTime.Now.AddMonths(-1), DateTime.Now)
        };
    }

    private async Task<WorkOrderMetricsDto> GetWorkOrderMetricsAsync()
    {
        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-1);

        var workOrders = await _workOrderService.GetWorkOrdersPagedAsync(new WorkOrderFilterDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = int.MaxValue
        });

        var completedWorkOrders = workOrders.Data.Where(wo => wo.Status == "Completed").ToList();

        return new WorkOrderMetricsDto
        {
            TotalWorkOrders = workOrders.Data.Count(),
            ActiveWorkOrders = workOrders.Data.Count(wo => wo.Status == "In Progress" || wo.Status == "Pending"),
            OverdueWorkOrders = workOrders.Data.Count(wo => wo.ScheduledEndDate < DateTime.Now && wo.Status != "Completed"),
            CompletedWorkOrders = completedWorkOrders.Count,
            WorkOrdersByStatus = workOrders.Data.GroupBy(wo => wo.Status).ToDictionary(g => g.Key, g => g.Count()),
            WorkOrdersByPriority = workOrders.Data.GroupBy(wo => wo.Priority).ToDictionary(g => g.Key, g => g.Count()),
            MTTR = CalculateMTTR(completedWorkOrders),
            FirstTimeFixRate = CalculateFirstTimeFixRate(completedWorkOrders)
        };
    }

    private async Task<ScheduleMetricsDto> GetScheduleMetricsAsync()
    {
        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-3);

        return new ScheduleMetricsDto
        {
            ComplianceRate = await CalculateScheduleComplianceAsync(startDate, endDate),
            PlannedMaintenancePercentage = await CalculatePreventiveMaintenanceRatioAsync(startDate, endDate)
        };
    }

    private async Task<TechnicianMetricsDto> GetTechnicianMetricsAsync()
    {
        var technicians = await _technicianService.GetAllTechniciansAsync();
        var availableTechnicians = await _technicianService.GetAvailableTechniciansAsync(DateTime.Now, DateTime.Now.AddHours(8));

        return new TechnicianMetricsDto
        {
            TotalTechnicians = technicians.Count(),
            AvailableTechnicians = availableTechnicians.Count()
        };
    }

    private async Task<SafetyMetricsDto> GetSafetyMetricsAsync()
    {
        var protocols = await _safetyProtocolService.GetAllProtocolsAsync();
        var activeProtocols = protocols.Where(p => p.IsActive).Count();

        return new SafetyMetricsDto
        {
            ActiveProtocols = activeProtocols,
            OverallComplianceRate = 95.5 // Mock data - would be calculated from actual adherence records
        };
    }

    private double CalculateMTTR(List<WorkOrderListDto> completedWorkOrders)
    {
        if (!completedWorkOrders.Any()) return 0;

        var totalRepairTime = completedWorkOrders
            .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            .Sum(wo => (wo.ActualEndDate.Value - wo.ActualStartDate.Value).TotalHours);

        return totalRepairTime / completedWorkOrders.Count;
    }

    private double CalculateFirstTimeFixRate(List<WorkOrderListDto> completedWorkOrders)
    {
        if (!completedWorkOrders.Any()) return 0;

        // Mock calculation - in reality would check for rework or repeat issues
        var firstTimeFixes = completedWorkOrders.Count(wo => !wo.Title.Contains("Rework"));
        return (double)firstTimeFixes / completedWorkOrders.Count * 100;
    }

    private async Task<double> CalculateScheduleComplianceAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - in reality would compare scheduled vs actual completion dates
        return 87.3;
    }

    private double CalculatePlannedMaintenancePercentage(IEnumerable<WorkOrderListDto> workOrders)
    {
        if (!workOrders.Any()) return 0;

        var plannedWorkOrders = workOrders.Count(wo => wo.WorkOrderSource == "Scheduled");
        return (double)plannedWorkOrders / workOrders.Count() * 100;
    }

    private async Task<double> CalculateMTBFAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Mean Time Between Failures
        return 720.5; // hours
    }

    private async Task<double> CalculateOEEAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Overall Equipment Effectiveness
        return 78.2; // percentage
    }

    private async Task<decimal> CalculateMaintenanceCostPerAssetAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation
        return 2350.75m;
    }

    private double CalculateAverageWorkOrderDuration(List<WorkOrderListDto> completedWorkOrders)
    {
        if (!completedWorkOrders.Any()) return 0;

        var totalDuration = completedWorkOrders
            .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            .Sum(wo => (wo.ActualEndDate.Value - wo.ActualStartDate.Value).TotalHours);

        return totalDuration / completedWorkOrders.Count;
    }

    private async Task<double> CalculatePreventiveMaintenanceRatioAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation
        return 65.8; // percentage
    }

    private async Task<List<UpcomingMaintenanceDto>> GetUpcomingMaintenanceAsync(int days)
    {
        // Mock data - would be retrieved from actual schedules
        return new List<UpcomingMaintenanceDto>
        {
            new UpcomingMaintenanceDto
            {
                AssetName = "Production Line A",
                MaintenanceType = "Monthly Inspection",
                DueDate = DateTime.Now.AddDays(2),
                Priority = "Medium"
            }
        };
    }

    private async Task<List<MaintenanceAlertDto>> GetRecentAlertsAsync(int count)
    {
        // Mock data - would be retrieved from actual alerts system
        return new List<MaintenanceAlertDto>
        {
            new MaintenanceAlertDto
            {
                Title = "Equipment Overdue for Maintenance",
                Message = "Conveyor Belt B is 3 days overdue for scheduled maintenance",
                Severity = "Warning",
                CreatedAt = DateTime.Now.AddHours(-2)
            }
        };
    }

    private async Task<List<TopIssueDto>> GetTopIssuesAsync(int count)
    {
        // Mock data - would be calculated from work order patterns
        return new List<TopIssueDto>
        {
            new TopIssueDto
            {
                Issue = "Motor Failure",
                Frequency = 15,
                AverageCost = 1250.00m,
                TotalCost = 18750.00m
            }
        };
    }

    private double CalculateTotalDowntime(List<WorkOrderListDto> workOrders)
    {
        // Mock calculation
        return workOrders.Sum(wo => wo.ActualHours ?? 0);
    }

    private double CalculateAverageRepairTime(List<WorkOrderListDto> workOrders)
    {
        return workOrders.Any() ? workOrders.Average(wo => wo.ActualHours ?? 0) : 0;
    }

    private double CalculateAssetAvailability(MaintenanceAssetDto asset, List<WorkOrderListDto> workOrders, DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Asset Availability
        return 92.5; // percentage
    }

    private double CalculateAssetReliability(List<WorkOrderListDto> workOrders, DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Asset Reliability
        return 88.7; // percentage
    }

    private List<string> GenerateAssetRecommendations(MaintenanceAssetDto asset, List<WorkOrderListDto> workOrders)
    {
        var recommendations = new List<string>();

        if (workOrders.Count(wo => wo.WorkOrderSource != "Scheduled") > workOrders.Count * 0.3)
            recommendations.Add("Consider increasing preventive maintenance frequency");

        if (workOrders.Any(wo => wo.ActualCost > 5000))
            recommendations.Add("Review high-cost maintenance patterns");

        return recommendations;
    }

    private decimal CalculateLaborCost(WorkOrderListDto workOrder)
    {
        // Mock calculation - would calculate from labor records
        return (decimal)(workOrder.ActualHours ?? 0) * 75m; // $75/hour average
    }

    private decimal CalculatePartsCost(WorkOrderListDto workOrder)
    {
        // Mock calculation - would calculate from parts usage
        return (workOrder.ActualCost ?? 0) * 0.6m; // Assume 60% is parts
    }

    private decimal CalculateContractorCost(WorkOrderListDto workOrder)
    {
        // Mock calculation - would calculate from contractor records
        return (workOrder.ActualCost ?? 0) * 0.15m; // Assume 15% is contractor cost
    }

    private Dictionary<string, decimal> GroupCostByCategory(List<WorkOrderListDto> workOrders)
    {
        // Mock grouping
        return new Dictionary<string, decimal>
        {
            ["Preventive"] = workOrders.Where(wo => wo.WorkOrderSource == "Scheduled").Sum(wo => wo.ActualCost ?? 0),
            ["Corrective"] = workOrders.Where(wo => wo.WorkOrderSource != "Scheduled").Sum(wo => wo.ActualCost ?? 0)
        };
    }

    private Dictionary<string, decimal> GroupCostByAsset(List<WorkOrderListDto> workOrders)
    {
        return workOrders
            .GroupBy(wo => wo.Asset?.Name ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Sum(wo => wo.ActualCost ?? 0));
    }

    private List<MonthlyCostDto> CalculateCostTrend(List<WorkOrderListDto> workOrders, DateTime startDate, DateTime endDate)
    {
        // Mock trend calculation
        return workOrders
            .GroupBy(wo => new { wo.CreatedAt.Year, wo.CreatedAt.Month })
            .Select(g => new MonthlyCostDto
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                TotalCost = g.Sum(wo => wo.ActualCost ?? 0)
            })
            .OrderBy(x => x.Month)
            .ToList();
    }

    private async Task<decimal> CalculateBudgetVarianceAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - would compare against budget
        return -15000m; // $15k under budget
    }

    #endregion
}

#region Supporting DTOs

public class AssetMetricsDto
{
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int CriticalAssets { get; set; }
    public int AssetsRequiringMaintenance { get; set; }
    public Dictionary<string, int> AssetsByStatus { get; set; } = new();
    public double MTBF { get; set; }
    public double OEE { get; set; }
}

public class WorkOrderMetricsDto
{
    public int TotalWorkOrders { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public Dictionary<string, int> WorkOrdersByStatus { get; set; } = new();
    public Dictionary<string, int> WorkOrdersByPriority { get; set; } = new();
    public double MTTR { get; set; }
    public double FirstTimeFixRate { get; set; }
}

public class ScheduleMetricsDto
{
    public double ComplianceRate { get; set; }
    public double PlannedMaintenancePercentage { get; set; }
}

public class TechnicianMetricsDto
{
    public int TotalTechnicians { get; set; }
    public int AvailableTechnicians { get; set; }
}

public class SafetyMetricsDto
{
    public int ActiveProtocols { get; set; }
    public double OverallComplianceRate { get; set; }
}

#endregion