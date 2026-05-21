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

                RecentAlerts = await GetRecentAlertsAsync(10),

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

            var completedWorkOrders = workOrders.Data.Where(wo => wo.Status == "Completed").ToList();

            return new MaintenanceCostAnalysisDto
            {
                ReportPeriod = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",

                TotalMaintenanceCost = completedWorkOrders.Sum(wo => wo.ActualCost),
                LaborCost = completedWorkOrders.Sum(wo => CalculateLaborCost(wo)),
                PartsCost = completedWorkOrders.Sum(wo => CalculatePartsCost(wo)),
                ContractorCost = completedWorkOrders.Sum(wo => CalculateContractorCost(wo)),

                PlannedMaintenanceCost = completedWorkOrders
                    .Where(wo => wo.WorkOrderSource == "Scheduled")
                    .Sum(wo => wo.ActualCost),

                UnplannedMaintenanceCost = completedWorkOrders
                    .Where(wo => wo.WorkOrderSource != "Scheduled")
                    .Sum(wo => wo.ActualCost),

                CostByCategory = new List<CostByCategoryDto>(), // TODO: Convert Dictionary to List
                CostByAsset = new List<CostByAssetDto>(), // TODO: Convert Dictionary to List
                CostTrend = new List<TrendDataPointDto>(), // TODO: Convert MonthlyCostDto to TrendDataPointDto

                BudgetVariance = (double)await CalculateBudgetVarianceAsync(startDate, endDate),
                CostPerWorkOrder = completedWorkOrders.Any() ?
                    (double)(completedWorkOrders.Sum(wo => wo.ActualCost) / completedWorkOrders.Count) : 0
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

    private static double CalculateMTTR(List<WorkOrderListDto> completedWorkOrders)
    {
        if (!completedWorkOrders.Any())
        {
            return 0;
        }

        var totalRepairTime = completedWorkOrders
            .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            .Sum(wo => (wo.ActualEndDate.Value - wo.ActualStartDate.Value).TotalHours);

        return totalRepairTime / completedWorkOrders.Count;
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

    private static async Task<double> CalculateScheduleComplianceAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - in reality would compare scheduled vs actual completion dates
        return 87.3;
    }

    private static double CalculatePlannedMaintenancePercentage(IEnumerable<WorkOrderListDto> workOrders)
    {
        if (!workOrders.Any())
        {
            return 0;
        }

        var plannedWorkOrders = workOrders.Count(wo => wo.WorkOrderSource == "Scheduled");
        return (double)plannedWorkOrders / workOrders.Count() * 100;
    }

    private static async Task<double> CalculateMTBFAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Mean Time Between Failures
        return 720.5; // hours
    }

    private static async Task<double> CalculateOEEAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - Overall Equipment Effectiveness
        return 78.2; // percentage
    }

    private static async Task<decimal> CalculateMaintenanceCostPerAssetAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation
        return 2350.75m;
    }

    private static double CalculateAverageWorkOrderDuration(List<WorkOrderListDto> completedWorkOrders)
    {
        if (!completedWorkOrders.Any())
        {
            return 0;
        }

        var totalDuration = completedWorkOrders
            .Where(wo => wo.ActualStartDate.HasValue && wo.ActualEndDate.HasValue)
            .Sum(wo => (wo.ActualEndDate.Value - wo.ActualStartDate.Value).TotalHours);

        return totalDuration / completedWorkOrders.Count;
    }

    private static async Task<double> CalculatePreventiveMaintenanceRatioAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation
        return 65.8; // percentage
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

    private static Dictionary<string, decimal> GroupCostByCategory(List<WorkOrderListDto> workOrders)
    {
        // Mock grouping
        return new Dictionary<string, decimal>
        {
            ["Preventive"] = workOrders.Where(wo => wo.WorkOrderSource == "Scheduled").Sum(wo => wo.ActualCost),
            ["Corrective"] = workOrders.Where(wo => wo.WorkOrderSource != "Scheduled").Sum(wo => wo.ActualCost)
        };
    }

    private static Dictionary<string, decimal> GroupCostByAsset(List<WorkOrderListDto> workOrders)
    {
        return workOrders
            .GroupBy(wo => wo.AssetName ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Sum(wo => wo.ActualCost));
    }

    private static List<MonthlyCostDto> CalculateCostTrend(List<WorkOrderListDto> workOrders, DateTime startDate, DateTime endDate)
    {
        // Mock trend calculation
        return workOrders
            .GroupBy(wo => new { wo.CreatedAt.Year, wo.CreatedAt.Month })
            .Select(g => new MonthlyCostDto
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                TotalCost = g.Sum(wo => wo.ActualCost)
            })
            .OrderBy(x => x.Month)
            .ToList();
    }

    private static async Task<decimal> CalculateBudgetVarianceAsync(DateTime startDate, DateTime endDate)
    {
        // Mock calculation - would compare against budget
        return -15000m; // base-currency variance under budget
    }

    #endregion

    #region Required Interface Methods

    public async Task<PreventiveMaintenanceReportDto> GetPreventiveMaintenanceReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Generating preventive maintenance report from {StartDate} to {EndDate}", startDate, endDate);

        // Mock implementation - would get actual PM data
        return new PreventiveMaintenanceReportDto
        {
            ReportDate = DateTime.UtcNow,
            TotalPreventiveTasks = 150,
            CompletedTasks = 142,
            OverdueTasks = 8,
            ComplianceRate = 94.7,
            TotalCost = 45000m,
            AssetBreakdown = new List<AssetPreventiveMaintenanceDto>(),
            TypeBreakdown = new List<MaintenanceTypeBreakdownDto>()
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
                wo.Status == "Completed" &&
                wo.ActualCompletionDate.HasValue &&
                wo.ActualCompletionDate.Value >= monthStart &&
                wo.ActualCompletionDate.Value <= monthEnd).ToList();

            var costInMonth = completedInMonth.Sum(wo => (double)wo.ActualCost);

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
                AverageCost = g.Any() ? g.Average(wo => wo.ActualCost) : 0,
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

        // Mock implementation - would calculate actual utilization
        return new TechnicianUtilizationReportDto
        {
            ReportDate = DateTime.UtcNow,
            TechnicianStats = GenerateMockTechnicianStats(),
            OverallUtilizationRate = 78.5,
            TotalLaborCost = 125000m,
            AverageProductivity = 85.2
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
