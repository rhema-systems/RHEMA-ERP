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

            // IMPORTANT: run analytics sequentially to avoid concurrent DbContext usage across services.
            // Each underlying service (assets, work orders, schedules, technicians, safety) uses the same scoped DbContext,
            // so parallel execution would trigger EF Core's concurrency detector.
            var assetMetrics = await GetAssetMetricsAsync();
            var workOrderMetrics = await GetWorkOrderMetricsAsync();
            var scheduleMetrics = await GetScheduleMetricsAsync();
            var technicianMetrics = await GetTechnicianMetricsAsync();
            var safetyMetrics = await GetSafetyMetricsAsync();

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
                    ComplianceRate = (decimal)(safetyMetrics?.OverallComplianceRate ?? 0)
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

                UpcomingMaintenance = new List<MaintenanceScheduleDto>(), // TODO: Convert UpcomingMaintenanceDto to MaintenanceScheduleDto

                RecentAlerts = await BuildRecentAlertsAsync(10),

                TopIssues = new List<string>() // TODO: Convert TopIssueDto to string list
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

            var completedWorkOrders = workOrders.Data.Where(wo => IsCompletedWorkOrderStatus(wo.Status)).ToList();
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

            var asset = await _assetService.GetAssetByIdAsync(assetId) ?? throw new ArgumentException($"Asset with ID {assetId} not found");
            var workOrders = await _workOrderService.GetWorkOrdersByAssetAsync(assetId);
            var periodWorkOrders = workOrders.Where(wo => wo.ScheduledStartDate >= startDate && wo.ScheduledStartDate <= endDate).ToList();

            return new AssetPerformanceReportDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                AssetNumber = asset.AssetNumber,
                ReportPeriod = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",

                TotalWorkOrders = periodWorkOrders.Count,
                CompletedWorkOrders = periodWorkOrders.Count(wo => wo.Status == "Completed"),
                PlannedWorkOrders = periodWorkOrders.Count(wo => wo.WorkOrderSource == "Scheduled"),
                UnplannedWorkOrders = periodWorkOrders.Count(wo => wo.WorkOrderSource != "Scheduled"),

                TotalDowntime = CalculateTotalDowntime(periodWorkOrders),
                AverageRepairTime = CalculateAverageRepairTime(periodWorkOrders),

                MaintenanceCost = periodWorkOrders.Sum(wo => wo.ActualCost),
                LaborHours = periodWorkOrders.Sum(wo => wo.ActualHours),

                Availability = CalculateAssetAvailability(asset, periodWorkOrders, startDate, endDate),
                Reliability = CalculateAssetReliability(periodWorkOrders, startDate, endDate),

                MaintenanceHistory = new List<MaintenanceHistoryItemDto>(), // TODO: Convert WorkOrderListDto to MaintenanceHistoryItemDto

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

            var periodWorkOrders = workOrders.Data.ToList();
            var completedWorkOrders = periodWorkOrders.Where(wo => IsCompletedWorkOrderStatus(wo.Status)).ToList();
            var costedWorkOrders = periodWorkOrders
                .Where(wo => (wo.ActualCost > 0 ? wo.ActualCost : wo.EstimatedCost) > 0)
                .ToList();
            var totalCost = costedWorkOrders.Sum(wo => wo.ActualCost > 0 ? wo.ActualCost : wo.EstimatedCost);
            var plannedCost = costedWorkOrders
                .Where(wo =>
                    string.Equals(wo.WorkOrderSource, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                    ContainsAny(wo.MaintenanceTypeName, "preventive", "planned", "scheduled") ||
                    ContainsAny(wo.WorkOrderTypeName, "preventive", "planned", "scheduled"))
                .Sum(wo => wo.ActualCost > 0 ? wo.ActualCost : wo.EstimatedCost);
            var unplannedCost = totalCost - plannedCost;

            return new MaintenanceCostAnalysisDto
            {
                ReportPeriod = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",

                TotalCost = totalCost,
                TotalMaintenanceCost = totalCost,
                PreventiveCost = plannedCost,
                CorrectiveCost = unplannedCost,
                PlannedMaintenanceCost = plannedCost,
                UnplannedMaintenanceCost = unplannedCost,
                LaborCost = 0,
                PartsCost = 0,
                MaterialsCost = 0,
                ContractorCost = 0,

                CostByCategory = BuildCostByCategory(costedWorkOrders, totalCost),
                CostByAsset = BuildCostByAsset(costedWorkOrders, totalCost),
                CostByMonth = BuildCostByMonth(costedWorkOrders),
                CostTrend = BuildCostTrend(costedWorkOrders, startDate, endDate),

                BudgetVariance = 0,
                CostPerWorkOrder = completedWorkOrders.Any() ?
                    (double)(totalCost / completedWorkOrders.Count) : 0
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
        var workOrders = await _workOrderService.GetWorkOrdersPagedAsync(new WorkOrderFilterDto
        {
            Page = 1,
            PageSize = int.MaxValue
        });

        var workOrderItems = workOrders.Data.ToList();
        var completedWorkOrders = workOrderItems.Where(wo => IsCompletedWorkOrderStatus(wo.Status)).ToList();

        return new WorkOrderMetricsDto
        {
            TotalWorkOrders = workOrderItems.Count,
            ActiveWorkOrders = workOrderItems.Count(wo => IsActiveWorkOrderStatus(wo.Status)),
            OverdueWorkOrders = workOrderItems.Count(wo => wo.IsOverdue),
            CompletedWorkOrders = completedWorkOrders.Count,
            WorkOrdersByStatus = workOrderItems
                .GroupBy(wo => NormalizeWorkOrderStatus(wo.Status))
                .ToDictionary(g => g.Key, g => g.Count()),
            WorkOrdersByPriority = workOrderItems
                .GroupBy(wo => string.IsNullOrWhiteSpace(wo.PriorityName) ? wo.Priority : wo.PriorityName)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(g => g.Key, g => g.Count()),
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
            OverallComplianceRate = 0
        };
    }

    private static string NormalizeWorkOrderStatus(string? status)
    {
        return status switch
        {
            "InProgress" => "In Progress",
            "OnHold" => "On Hold",
            "PendingQualityApproval" => "Pending QA",
            null or "" => "Unspecified",
            _ => status
        };
    }

    private static bool IsActiveWorkOrderStatus(string? status)
    {
        return !IsCompletedWorkOrderStatus(status) && !IsCancelledWorkOrderStatus(status);
    }

    private static bool IsCompletedWorkOrderStatus(string? status)
    {
        return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCancelledWorkOrderStatus(string? status)
    {
        return string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string? value, params string[] terms)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static double CalculateMTTR(List<WorkOrderListDto> completedWorkOrders)
    {
        var repairTimes = completedWorkOrders
            .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            .Select(wo => (wo.ActualEndDate!.Value - wo.ActualStartDate!.Value).TotalHours)
            .Where(hours => hours >= 0)
            .ToList();

        return repairTimes.Any() ? repairTimes.Average() : 0;
    }

    private static double CalculateFirstTimeFixRate(List<WorkOrderListDto> completedWorkOrders)
    {
        if (!completedWorkOrders.Any())
        {
            return 0;
        }

        // Mock calculation - in reality would check for rework or repeat issues
        var firstTimeFixes = completedWorkOrders.Count(wo => !wo.Title.Contains("Rework"));
        return (double)firstTimeFixes / completedWorkOrders.Count * 100;
    }

    private async Task<double> CalculateScheduleComplianceAsync(DateTime startDate, DateTime endDate)
    {
        var schedules = await _scheduleService.GetSchedulesPagedAsync(new MaintenanceScheduleFilterDto
        {
            DueDateFrom = startDate,
            DueDateTo = endDate,
            Page = 1,
            PageSize = int.MaxValue
        });

        var scheduleItems = schedules.Items.ToList();
        if (!scheduleItems.Any())
        {
            return 0;
        }

        var overdueSchedules = scheduleItems.Count(schedule => schedule.IsOverdue);
        return (double)(scheduleItems.Count - overdueSchedules) / scheduleItems.Count * 100;
    }

    private static double CalculatePlannedMaintenancePercentage(IEnumerable<WorkOrderListDto> workOrders)
    {
        if (!workOrders.Any())
        {
            return 0;
        }

        var plannedWorkOrders = workOrders.Count(wo =>
            string.Equals(wo.WorkOrderSource, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(wo.MaintenanceTypeName, "preventive", "planned", "scheduled") ||
            ContainsAny(wo.WorkOrderTypeName, "preventive", "planned", "scheduled"));
        return (double)plannedWorkOrders / workOrders.Count() * 100;
    }

    private async Task<double> CalculateMTBFAsync(DateTime startDate, DateTime endDate)
    {
        var workOrders = await GetWorkOrdersInPeriodAsync(startDate, endDate);
        var unplannedWorkOrders = workOrders
            .Where(wo => !string.Equals(wo.WorkOrderSource, "Scheduled", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!unplannedWorkOrders.Any())
        {
            return 0;
        }

        var assetMetrics = await _assetService.GetAssetMetricsAsync();
        var trackedAssetCount = Math.Max(assetMetrics.TotalAssets, 1);
        var periodHours = Math.Max((endDate - startDate).TotalHours, 0);

        return periodHours * trackedAssetCount / unplannedWorkOrders.Count;
    }

    private async Task<double> CalculateOEEAsync(DateTime startDate, DateTime endDate)
    {
        var assetMetrics = await _assetService.GetAssetMetricsAsync();
        if (assetMetrics.TotalAssets <= 0)
        {
            return 0;
        }

        return (double)assetMetrics.ActiveAssets / assetMetrics.TotalAssets * 100;
    }

    private async Task<decimal> CalculateMaintenanceCostPerAssetAsync(DateTime startDate, DateTime endDate)
    {
        var workOrders = await GetWorkOrdersInPeriodAsync(startDate, endDate);
        var assetMetrics = await _assetService.GetAssetMetricsAsync();
        if (assetMetrics.TotalAssets <= 0)
        {
            return 0;
        }

        var totalCost = workOrders.Sum(wo => wo.ActualCost > 0 ? wo.ActualCost : wo.EstimatedCost);
        return totalCost / assetMetrics.TotalAssets;
    }

    private static double CalculateAverageWorkOrderDuration(List<WorkOrderListDto> completedWorkOrders)
    {
        var durations = completedWorkOrders
            .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            .Select(wo => (wo.ActualEndDate!.Value - wo.ActualStartDate!.Value).TotalHours)
            .Where(hours => hours >= 0)
            .ToList();

        return durations.Any() ? durations.Average() : 0;
    }

    private async Task<double> CalculatePreventiveMaintenanceRatioAsync(DateTime startDate, DateTime endDate)
    {
        var workOrders = await GetWorkOrdersInPeriodAsync(startDate, endDate);
        if (!workOrders.Any())
        {
            return 0;
        }

        var plannedWorkOrders = workOrders.Count(wo =>
            string.Equals(wo.WorkOrderSource, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(wo.MaintenanceTypeName, "preventive", "planned", "scheduled") ||
            ContainsAny(wo.WorkOrderTypeName, "preventive", "planned", "scheduled"));

        return (double)plannedWorkOrders / workOrders.Count * 100;
    }

    private static async Task<List<UpcomingMaintenanceDto>> GetUpcomingMaintenanceAsync(int days)
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

    private async Task<List<MaintenanceAlertDto>> BuildRecentAlertsAsync(int count)
    {
        var alerts = new List<MaintenanceAlertDto>();
        var overdueWorkOrders = await _workOrderService.GetOverdueWorkOrdersAsync();
        foreach (var workOrder in overdueWorkOrders.Take(count))
        {
            alerts.Add(new MaintenanceAlertDto
            {
                Id = workOrder.Id,
                AlertType = "OverdueWorkOrder",
                Title = $"Overdue work order {workOrder.WorkOrderNumber}",
                Message = $"{workOrder.Title} is past its requested completion date.",
                Priority = workOrder.PriorityName,
                Severity = "Warning",
                CreatedDate = workOrder.RequestedCompletionDate ?? workOrder.CreatedAt,
                CreatedAt = workOrder.RequestedCompletionDate ?? workOrder.CreatedAt,
                AssetId = workOrder.AssetId,
                AssetName = workOrder.AssetName
            });
        }

        if (alerts.Count < count)
        {
            var dueAssets = await _assetService.GetAssetsRequiringMaintenanceAsync();
            alerts.AddRange(dueAssets
                .Take(count - alerts.Count)
                .Select(asset => new MaintenanceAlertDto
                {
                    Id = asset.Id,
                    AlertType = "AssetMaintenanceDue",
                    Title = $"Maintenance due for {asset.Name}",
                    Message = $"{asset.AssetNumber} is due for scheduled maintenance.",
                    Priority = "Normal",
                    Severity = "Info",
                    CreatedDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    AssetId = asset.Id,
                    AssetName = asset.Name
                }));
        }

        return alerts
            .OrderByDescending(alert => alert.CreatedAt == default ? alert.CreatedDate : alert.CreatedAt)
            .Take(count)
            .ToList();
    }

    private static async Task<List<MaintenanceAlertDto>> GetRecentAlertsAsync(int count)
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

    private static async Task<List<TopIssueDto>> GetTopIssuesAsync(int count)
    {
        // Mock data - would be calculated from work order patterns
        return new List<TopIssueDto>
        {
            new TopIssueDto
            {
                Issue = "Motor Failure",
                Frequency = 15,
                AverageCost = 1250.00,
                TotalCost = 18750.00
            }
        };
    }

    private static double CalculateTotalDowntime(List<WorkOrderListDto> workOrders)
    {
        // Mock calculation
        return workOrders.Sum(wo => wo.ActualHours);
    }

    private static double CalculateAverageRepairTime(List<WorkOrderListDto> workOrders)
    {
        return workOrders.Any() ? workOrders.Average(wo => wo.ActualHours) : 0;
    }

    private static double CalculateAssetAvailability(MaintenanceAssetDto asset, List<WorkOrderListDto> workOrders, DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Asset Availability
        return 92.5; // percentage
    }

    private static double CalculateAssetReliability(List<WorkOrderListDto> workOrders, DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Asset Reliability
        return 88.7; // percentage
    }

    private static List<string> GenerateAssetRecommendations(MaintenanceAssetDto asset, List<WorkOrderListDto> workOrders)
    {
        var recommendations = new List<string>();

        if (workOrders.Count(wo => wo.WorkOrderSource != "Scheduled") > workOrders.Count * 0.3)
        {
            recommendations.Add("Consider increasing preventive maintenance frequency");
        }

        if (workOrders.Any(wo => wo.ActualCost > 5000))
        {
            recommendations.Add("Review high-cost maintenance patterns");
        }

        return recommendations;
    }

    private static decimal CalculateLaborCost(WorkOrderListDto workOrder)
    {
        // Mock calculation - would calculate from labor records
        return (decimal)workOrder.ActualHours * 75m; // base-currency hourly average
    }

    private static decimal CalculatePartsCost(WorkOrderListDto workOrder)
    {
        // Mock calculation - would calculate from parts usage
        return workOrder.ActualCost * 0.6m; // Assume 60% is parts
    }

    private static decimal CalculateContractorCost(WorkOrderListDto workOrder)
    {
        // Mock calculation - would calculate from contractor records
        return workOrder.ActualCost * 0.15m; // Assume 15% is contractor cost
    }

    private static List<CostByCategoryDto> BuildCostByCategory(List<WorkOrderListDto> workOrders, decimal totalCost)
    {
        return workOrders
            .GroupBy(wo => string.IsNullOrWhiteSpace(wo.MaintenanceTypeName) ? "Unspecified" : wo.MaintenanceTypeName)
            .Select(g =>
            {
                var cost = g.Sum(wo => GetWorkOrderCost(wo));
                return new CostByCategoryDto
                {
                    Category = g.Key,
                    Cost = cost,
                    Percentage = totalCost > 0 ? cost / totalCost * 100 : 0
                };
            })
            .OrderByDescending(x => x.Cost)
            .ToList();
    }

    private static List<CostByAssetDto> BuildCostByAsset(List<WorkOrderListDto> workOrders, decimal totalCost)
    {
        return workOrders
            .GroupBy(wo => new { wo.AssetId, AssetName = string.IsNullOrWhiteSpace(wo.AssetName) ? "Unknown Asset" : wo.AssetName })
            .Select(g =>
            {
                var cost = g.Sum(GetWorkOrderCost);
                return new CostByAssetDto
                {
                    AssetId = g.Key.AssetId,
                    AssetName = g.Key.AssetName,
                    Cost = cost,
                    Percentage = totalCost > 0 ? cost / totalCost * 100 : 0
                };
            })
            .OrderByDescending(x => x.Cost)
            .Take(10)
            .ToList();
    }

    private static List<CostByMonthDto> BuildCostByMonth(List<WorkOrderListDto> workOrders)
    {
        return workOrders
            .GroupBy(wo => new { wo.CreatedAt.Year, wo.CreatedAt.Month })
            .Select(g => new CostByMonthDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Cost = g.Sum(GetWorkOrderCost)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();
    }

    private static List<TrendDataPointDto> BuildCostTrend(List<WorkOrderListDto> workOrders, DateTime startDate, DateTime endDate)
    {
        var trend = new List<TrendDataPointDto>();
        var monthStart = new DateTime(startDate.Year, startDate.Month, 1);
        var finalMonth = new DateTime(endDate.Year, endDate.Month, 1);

        while (monthStart <= finalMonth)
        {
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
            var cost = workOrders
                .Where(wo => wo.CreatedAt >= monthStart && wo.CreatedAt <= monthEnd)
                .Sum(wo => (double)GetWorkOrderCost(wo));

            trend.Add(new TrendDataPointDto
            {
                Date = monthStart,
                Value = cost,
                MetricType = "Cost",
                Label = monthStart.ToString("MMM yyyy")
            });

            monthStart = monthStart.AddMonths(1);
        }

        return trend;
    }

    private static decimal GetWorkOrderCost(WorkOrderListDto workOrder)
    {
        return workOrder.ActualCost > 0 ? workOrder.ActualCost : workOrder.EstimatedCost;
    }

    #endregion

    #region Required Interface Methods

    public async Task<PreventiveMaintenanceReportDto> GetPreventiveMaintenanceReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Generating preventive maintenance report from {StartDate} to {EndDate}", startDate, endDate);

        var workOrders = await GetWorkOrdersInPeriodAsync(startDate, endDate);
        var preventiveWorkOrders = workOrders
            .Where(wo =>
                string.Equals(wo.WorkOrderSource, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                ContainsAny(wo.MaintenanceTypeName, "preventive", "planned", "scheduled") ||
                ContainsAny(wo.WorkOrderTypeName, "preventive", "planned", "scheduled"))
            .ToList();
        var completedPreventive = preventiveWorkOrders.Count(wo => IsCompletedWorkOrderStatus(wo.Status));
        var overduePreventive = preventiveWorkOrders.Count(wo => wo.IsOverdue);
        var totalPreventive = preventiveWorkOrders.Count;

        return new PreventiveMaintenanceReportDto
        {
            ReportDate = DateTime.UtcNow,
            TotalPreventiveTasks = totalPreventive,
            CompletedTasks = completedPreventive,
            OverdueTasks = overduePreventive,
            ComplianceRate = totalPreventive > 0 ? (double)completedPreventive / totalPreventive * 100 : 0,
            TotalCost = preventiveWorkOrders.Sum(GetWorkOrderCost),
            AssetBreakdown = preventiveWorkOrders
                .GroupBy(wo => new { wo.AssetId, wo.AssetName })
                .Select(g =>
                {
                    var assetTotal = g.Count();
                    var assetCompleted = g.Count(wo => IsCompletedWorkOrderStatus(wo.Status));
                    return new AssetPreventiveMaintenanceDto
                    {
                        AssetId = g.Key.AssetId,
                        AssetName = string.IsNullOrWhiteSpace(g.Key.AssetName) ? "Unknown Asset" : g.Key.AssetName,
                        ScheduledTasks = assetTotal,
                        CompletedTasks = assetCompleted,
                        OverdueTasks = g.Count(wo => wo.IsOverdue),
                        ComplianceRate = assetTotal > 0 ? (double)assetCompleted / assetTotal * 100 : 0,
                        TotalCost = g.Sum(GetWorkOrderCost)
                    };
                })
                .OrderByDescending(x => x.ScheduledTasks)
                .Take(10)
                .ToList(),
            TypeBreakdown = preventiveWorkOrders
                .GroupBy(wo => string.IsNullOrWhiteSpace(wo.MaintenanceTypeName) ? "Preventive" : wo.MaintenanceTypeName)
                .Select(g =>
                {
                    var count = g.Count();
                    return new MaintenanceTypeBreakdownDto
                    {
                        MaintenanceType = g.Key,
                        Count = count,
                        Percentage = totalPreventive > 0 ? (double)count / totalPreventive * 100 : 0,
                        AverageCost = count > 0 ? g.Average(GetWorkOrderCost) : 0,
                        AverageCompletionTime = CalculateAverageWorkOrderDuration(g.Where(wo => IsCompletedWorkOrderStatus(wo.Status)).ToList())
                    };
                })
                .OrderByDescending(x => x.Count)
                .ToList()
        };
    }

    public async Task<WorkOrderTrendsDto> GetWorkOrderTrendsAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Generating work order trends from {StartDate} to {EndDate}", startDate, endDate);

        // Fetch all work orders in the requested period using the existing service (EF-safe)
        var workOrders = await GetWorkOrdersInPeriodAsync(startDate, endDate);

        // Normalize to month buckets so the UI can display a clean "Monthly" trend.
        // We use the first day of each month as the representative date for that bucket.
        var monthStart = new DateTime(startDate.Year, startDate.Month, 1);
        var finalEnd = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(1).AddTicks(-1);

        var creationTrend = new List<TrendDataPointDto>();
        var completionTrend = new List<TrendDataPointDto>();
        var costTrend = new List<TrendDataPointDto>();

        while (monthStart <= finalEnd)
        {
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            var createdInMonth = workOrders.Where(wo => wo.CreatedAt >= monthStart && wo.CreatedAt <= monthEnd).ToList();
            var completedInMonth = workOrders.Where(wo =>
                IsCompletedWorkOrderStatus(wo.Status) &&
                wo.ActualCompletionDate.HasValue &&
                wo.ActualCompletionDate.Value >= monthStart &&
                wo.ActualCompletionDate.Value <= monthEnd).ToList();

            var costInMonth = completedInMonth.Sum(wo => (double)GetWorkOrderCost(wo));

            creationTrend.Add(new TrendDataPointDto
            {
                Date = monthStart,
                Value = createdInMonth.Count,
                MetricType = "Created",
                Label = monthStart.ToString("MMM yyyy")
            });

            completionTrend.Add(new TrendDataPointDto
            {
                Date = monthStart,
                Value = completedInMonth.Count,
                MetricType = "Completed",
                Label = monthStart.ToString("MMM yyyy")
            });

            costTrend.Add(new TrendDataPointDto
            {
                Date = monthStart,
                Value = costInMonth,
                MetricType = "Cost",
                Label = monthStart.ToString("MMM yyyy")
            });

            monthStart = monthStart.AddMonths(1);
        }

        // Work order type distribution over the entire period
        var totalWorkOrders = workOrders.Count;
        var typeDistribution = workOrders
            .GroupBy(wo => string.IsNullOrWhiteSpace(wo.MaintenanceTypeName)
                ? "Unspecified"
                : wo.MaintenanceTypeName)
            .Select(g => new WorkOrderTypeMetricDto
            {
                WorkOrderType = g.Key,
                Count = g.Count(),
                Percentage = totalWorkOrders > 0 ? (double)g.Count() / totalWorkOrders * 100 : 0,
                AverageCost = g.Any() ? g.Average(GetWorkOrderCost) : 0,
                AverageCompletionTime = g
                    .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
                    .Select(wo => (wo.ActualEndDate!.Value - wo.ActualStartDate!.Value).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new WorkOrderTrendsDto
        {
            StartDate = startDate,
            EndDate = endDate,
            CreationTrend = creationTrend,
            CompletionTrend = completionTrend,
            CostTrend = costTrend,
            TypeDistribution = typeDistribution
        };
    }

    public async Task<TechnicianUtilizationReportDto> GetTechnicianUtilizationReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Generating technician utilization report from {StartDate} to {EndDate}", startDate, endDate);

        var workOrders = await GetWorkOrdersInPeriodAsync(startDate, endDate);
        var assignedWorkOrders = workOrders
            .Where(wo => wo.AssignedTechnicianId.HasValue)
            .ToList();
        var completedWorkOrders = assignedWorkOrders
            .Where(wo => IsCompletedWorkOrderStatus(wo.Status))
            .ToList();
        var technicianStats = assignedWorkOrders
            .GroupBy(wo => new
            {
                TechnicianId = wo.AssignedTechnicianId!.Value,
                TechnicianName = string.IsNullOrWhiteSpace(wo.AssignedTechnicianName) ? "Unassigned" : wo.AssignedTechnicianName
            })
            .Select(g =>
            {
                var assignedCount = g.Count();
                var completedCount = g.Count(wo => IsCompletedWorkOrderStatus(wo.Status));
                var completedForTech = g.Where(wo => IsCompletedWorkOrderStatus(wo.Status)).ToList();
                return new TechnicianUtilizationDto
                {
                    TechnicianId = g.Key.TechnicianId,
                    TechnicianName = g.Key.TechnicianName,
                    AssignedWorkOrders = assignedCount,
                    CompletedWorkOrders = completedCount,
                    UtilizationRate = assignedCount > 0 ? (double)completedCount / assignedCount * 100 : 0,
                    AverageCompletionTime = CalculateAverageWorkOrderDuration(completedForTech),
                    TotalLaborCost = 0,
                    ProductivityScore = assignedCount > 0 ? (double)completedCount / assignedCount * 100 : 0
                };
            })
            .OrderByDescending(x => x.CompletedWorkOrders)
            .ToList();

        return new TechnicianUtilizationReportDto
        {
            ReportDate = DateTime.UtcNow,
            TechnicianStats = technicianStats,
            OverallUtilizationRate = assignedWorkOrders.Any()
                ? (double)completedWorkOrders.Count / assignedWorkOrders.Count * 100
                : 0,
            TotalLaborCost = 0,
            AverageProductivity = technicianStats.Any() ? technicianStats.Average(x => x.ProductivityScore) : 0
        };
    }

    public async Task<AssetReliabilityReportDto> GetAssetReliabilityReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Generating asset reliability report from {StartDate} to {EndDate}", startDate, endDate);

        // Mock implementation - would analyze actual reliability data
        return new AssetReliabilityReportDto
        {
            ReportDate = DateTime.UtcNow,
            OverallReliability = 92.3,
            AssetReliabilities = GenerateMockAssetReliabilities(),
            FailureModes = GenerateMockFailureModes(),
            ReliabilityTrend = new ReliabilityTrendDto
            {
                ReliabilityTrend = GenerateMockTrendData(startDate, endDate, "Reliability"),
                MTBFTrend = GenerateMockTrendData(startDate, endDate, "MTBF"),
                MTTRTrend = GenerateMockTrendData(startDate, endDate, "MTTR"),
                AvailabilityTrend = GenerateMockTrendData(startDate, endDate, "Availability")
            }
        };
    }

    #endregion

    #region Helper Methods for Mock Data

    private static List<TrendDataPointDto> GenerateMockTrendData(DateTime startDate, DateTime endDate, string metricType)
    {
        var trendData = new List<TrendDataPointDto>();
        var currentDate = startDate;
        var random = new Random();

        while (currentDate <= endDate)
        {
            trendData.Add(new TrendDataPointDto
            {
                Date = currentDate,
                Value = metricType switch
                {
                    "Created" => random.Next(10, 50),
                    "Completed" => random.Next(8, 45),
                    "Cost" => random.Next(5000, 25000),
                    "Reliability" => random.Next(85, 98),
                    "MTBF" => random.Next(120, 240),
                    "MTTR" => random.Next(2, 8),
                    "Availability" => random.Next(90, 99),
                    _ => random.Next(1, 100)
                },
                MetricType = metricType
            });
            currentDate = currentDate.AddDays(7); // Weekly data points
        }

        return trendData;
    }

    private static List<WorkOrderTypeMetricDto> GenerateMockTypeDistribution()
    {
        return new List<WorkOrderTypeMetricDto>
        {
            new() { WorkOrderType = "Preventive", Count = 45, Percentage = 52.3, AverageCost = 1200m, AverageCompletionTime = 2.5 },
            new() { WorkOrderType = "Corrective", Count = 25, Percentage = 29.1, AverageCost = 2800m, AverageCompletionTime = 4.2 },
            new() { WorkOrderType = "Emergency", Count = 8, Percentage = 9.3, AverageCost = 5200m, AverageCompletionTime = 1.8 },
            new() { WorkOrderType = "Inspection", Count = 8, Percentage = 9.3, AverageCost = 400m, AverageCompletionTime = 1.0 }
        };
    }

    private static List<TechnicianUtilizationDto> GenerateMockTechnicianStats()
    {
        return new List<TechnicianUtilizationDto>
        {
            new() { TechnicianId = Guid.NewGuid(), TechnicianName = "John Smith", UtilizationRate = 85.2, AssignedWorkOrders = 28, CompletedWorkOrders = 26, AverageCompletionTime = 3.2, TotalLaborCost = 12500m, ProductivityScore = 88.5 },
            new() { TechnicianId = Guid.NewGuid(), TechnicianName = "Sarah Johnson", UtilizationRate = 78.9, AssignedWorkOrders = 24, CompletedWorkOrders = 22, AverageCompletionTime = 2.8, TotalLaborCost = 11200m, ProductivityScore = 92.1 },
            new() { TechnicianId = Guid.NewGuid(), TechnicianName = "Mike Davis", UtilizationRate = 72.4, AssignedWorkOrders = 22, CompletedWorkOrders = 20, AverageCompletionTime = 3.8, TotalLaborCost = 10800m, ProductivityScore = 76.3 }
        };
    }

    private static List<AssetReliabilityDto> GenerateMockAssetReliabilities()
    {
        return new List<AssetReliabilityDto>
        {
            new() { AssetId = Guid.NewGuid(), AssetName = "Conveyor Belt A1", ReliabilityScore = 94.5, MTBF = 180.5, MTTR = 3.2, Availability = 98.2, FailureCount = 2, TotalDowntime = 6.4 },
            new() { AssetId = Guid.NewGuid(), AssetName = "Pump Station B2", ReliabilityScore = 89.2, MTBF = 145.2, MTTR = 4.8, Availability = 96.8, FailureCount = 3, TotalDowntime = 14.4 },
            new() { AssetId = Guid.NewGuid(), AssetName = "Generator C3", ReliabilityScore = 96.8, MTBF = 220.1, MTTR = 2.1, Availability = 99.1, FailureCount = 1, TotalDowntime = 2.1 }
        };
    }

    private static List<FailureModeAnalysisDto> GenerateMockFailureModes()
    {
        return new List<FailureModeAnalysisDto>
        {
            new() { FailureMode = "Mechanical Wear", Frequency = 15, Percentage = 42.9, AverageDowntime = 4.2, AverageCost = 2800m, Criticality = "High" },
            new() { FailureMode = "Electrical Fault", Frequency = 8, Percentage = 22.9, AverageDowntime = 6.5, AverageCost = 4200m, Criticality = "Critical" },
            new() { FailureMode = "Lubrication Issue", Frequency = 7, Percentage = 20.0, AverageDowntime = 2.1, AverageCost = 800m, Criticality = "Medium" },
            new() { FailureMode = "Calibration Drift", Frequency = 5, Percentage = 14.2, AverageDowntime = 1.5, AverageCost = 600m, Criticality = "Low" }
        };
    }

    private async Task<List<WorkOrderListDto>> GetWorkOrdersInPeriodAsync(DateTime startDate, DateTime endDate)
    {
        var result = await _workOrderService.GetWorkOrdersPagedAsync(new WorkOrderFilterDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = int.MaxValue
        });

        return result.Data.ToList();
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
