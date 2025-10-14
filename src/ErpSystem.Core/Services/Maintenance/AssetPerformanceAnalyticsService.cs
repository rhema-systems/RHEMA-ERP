using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Enhanced asset performance analytics service with advanced KPI calculations and performance metrics
/// </summary>
public class AssetPerformanceAnalyticsService : IAssetPerformanceAnalyticsService
{
    private readonly IMaintenanceAssetService _assetService;
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly ITechnicianService _technicianService;
    private readonly IIoTMaintenanceService _iotService;
    private readonly IMaintenanceAnalyticsService _analyticsService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<AssetPerformanceAnalyticsService> _logger;

    public AssetPerformanceAnalyticsService(
        IMaintenanceAssetService assetService,
        IWorkOrderService workOrderService,
        IMaintenanceScheduleService scheduleService,
        ITechnicianService technicianService,
        IIoTMaintenanceService iotService,
        IMaintenanceAnalyticsService analyticsService,
        ICurrentUserProvider currentUserProvider,
        ILogger<AssetPerformanceAnalyticsService> logger)
    {
        _assetService = assetService;
        _workOrderService = workOrderService;
        _scheduleService = scheduleService;
        _technicianService = technicianService;
        _iotService = iotService;
        _analyticsService = analyticsService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region Overall Equipment Effectiveness (OEE) Analytics

    public async Task<OeeAnalysisDto> CalculateOeeAsync(
        Guid assetId, DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Calculating OEE for asset {AssetId} from {StartDate} to {EndDate}", 
                assetId, startDate, endDate);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {assetId} not found");

            var workOrders = await _workOrderService.GetWorkOrdersByAssetAsync(assetId);
            var periodWorkOrders = workOrders.Where(wo => wo.CreatedAt >= startDate && wo.CreatedAt <= endDate);

            // Calculate OEE components
            var availability = await CalculateAvailabilityAsync(assetId, startDate, endDate, periodWorkOrders);
            var performance = await CalculatePerformanceAsync(assetId, startDate, endDate);
            var quality = await CalculateQualityAsync(assetId, startDate, endDate);

            var oeeScore = (availability / 100.0) * (performance / 100.0) * (quality / 100.0) * 100.0;

            return new OeeAnalysisDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                StartDate = startDate,
                EndDate = endDate,
                Availability = availability,
                Performance = performance,
                Quality = quality,
                OeeScore = oeeScore,
                PlannedProductionTime = await GetPlannedProductionTimeAsync(assetId, startDate, endDate),
                ActualRunTime = await GetActualRunTimeAsync(assetId, startDate, endDate),
                IdealCycleTime = await GetIdealCycleTimeAsync(assetId),
                ActualCycleTime = await GetActualCycleTimeAsync(assetId, startDate, endDate),
                GoodUnits = await GetGoodUnitsAsync(assetId, startDate, endDate),
                TotalUnits = await GetTotalUnitsAsync(assetId, startDate, endDate),
                Losses = await CalculateOeeLossesAsync(assetId, startDate, endDate, periodWorkOrders),
                PerformanceCategory = DeterminePerformanceCategory(oeeScore)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating OEE for asset {AssetId}", assetId);
            throw;
        }
    }

    public async Task<List<OeeAnalysisDto>> CalculateFleetOeeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Calculating fleet OEE from {StartDate} to {EndDate}", startDate, endDate);

            var assets = await _assetService.GetAllAssetsAsync();
            var oeeAnalyses = new List<OeeAnalysisDto>();

            foreach (var asset in assets)
            {
                var oeeAnalysis = await CalculateOeeAsync(asset.Id, startDate, endDate);
                oeeAnalyses.Add(oeeAnalysis);
            }

            return oeeAnalyses.OrderByDescending(o => o.OeeScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating fleet OEE");
            throw;
        }
    }

    #endregion

    #region Asset Reliability Analytics

    public async Task<AssetReliabilityMetricsDto> CalculateReliabilityMetricsAsync(
        Guid assetId, DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Calculating reliability metrics for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {assetId} not found");

            var workOrders = await _workOrderService.GetWorkOrdersByAssetAsync(assetId);
            var periodWorkOrders = workOrders.Where(wo => wo.CreatedAt >= startDate && wo.CreatedAt <= endDate);

            var failures = periodWorkOrders.Where(wo => 
                wo.Type == "Emergency" || wo.Type == "Corrective").ToList();

            var mtbf = await CalculateMeanTimeBetweenFailuresAsync(assetId, failures);
            var mttr = await CalculateMeanTimeToRepairAsync(assetId, failures);
            var availability = await CalculateReliabilityAvailabilityAsync(mtbf, mttr);
            
            return new AssetReliabilityMetricsDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                AnalysisPeriodStart = startDate,
                AnalysisPeriodEnd = endDate,
                MeanTimeBetweenFailures = mtbf,
                MeanTimeToRepair = mttr,
                MeanTimeToFailure = await CalculateMeanTimeToFailureAsync(assetId, failures),
                Availability = availability,
                ReliabilityScore = CalculateReliabilityScore(mtbf, mttr, availability),
                FailureRate = failures.Count / ((endDate - startDate).TotalDays / 365.25), // failures per year
                TotalFailures = failures.Count,
                PlannedDowntimeHours = await GetPlannedDowntimeHoursAsync(assetId, startDate, endDate),
                UnplannedDowntimeHours = await GetUnplannedDowntimeHoursAsync(assetId, failures),
                OperatingHours = await GetOperatingHoursAsync(assetId, startDate, endDate),
                FailureModes = await AnalyzeFailureModesAsync(assetId, failures),
                TrendAnalysis = await PerformReliabilityTrendAnalysisAsync(assetId, workOrders)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating reliability metrics for asset {AssetId}", assetId);
            throw;
        }
    }

    public async Task<List<AssetReliabilityRankingDto>> GetAssetReliabilityRankingsAsync(
        DateTime startDate, DateTime endDate, int topCount = 10)
    {
        try
        {
            _logger.LogInformation("Getting asset reliability rankings");

            var assets = await _assetService.GetAllAssetsAsync();
            var rankings = new List<AssetReliabilityRankingDto>();

            foreach (var asset in assets)
            {
                var metrics = await CalculateReliabilityMetricsAsync(asset.Id, startDate, endDate);
                
                rankings.Add(new AssetReliabilityRankingDto
                {
                    AssetId = asset.Id,
                    AssetName = asset.Name,
                    AssetType = asset.AssetType ?? "Unknown",
                    ReliabilityScore = metrics.ReliabilityScore,
                    Availability = metrics.Availability,
                    MeanTimeBetweenFailures = metrics.MeanTimeBetweenFailures,
                    MeanTimeToRepair = metrics.MeanTimeToRepair,
                    TotalFailures = metrics.TotalFailures,
                    FailureRate = metrics.FailureRate,
                    PerformanceCategory = DetermineReliabilityCategory(metrics.ReliabilityScore)
                });
            }

            return rankings.OrderByDescending(r => r.ReliabilityScore)
                          .Take(topCount)
                          .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset reliability rankings");
            throw;
        }
    }

    #endregion

    #region Performance Benchmarking

    public async Task<AssetPerformanceBenchmarkDto> GetPerformanceBenchmarkAsync(
        Guid assetId, string benchmarkCategory = "Industry")
    {
        try
        {
            _logger.LogInformation("Getting performance benchmark for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {assetId} not found");

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddMonths(-12); // Last 12 months

            var oeeAnalysis = await CalculateOeeAsync(assetId, startDate, endDate);
            var reliabilityMetrics = await CalculateReliabilityMetricsAsync(assetId, startDate, endDate);

            // Get benchmark data (mock implementation - would come from industry data)
            var benchmarkData = await GetBenchmarkDataAsync(asset.AssetType ?? "General", benchmarkCategory);

            return new AssetPerformanceBenchmarkDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                AssetType = asset.AssetType ?? "Unknown",
                BenchmarkCategory = benchmarkCategory,
                BenchmarkDate = DateTime.UtcNow,
                
                // Current Performance
                CurrentOee = oeeAnalysis.OeeScore,
                CurrentAvailability = reliabilityMetrics.Availability,
                CurrentReliabilityScore = reliabilityMetrics.ReliabilityScore,
                CurrentMtbf = reliabilityMetrics.MeanTimeBetweenFailures,
                CurrentMttr = reliabilityMetrics.MeanTimeToRepair,
                
                // Benchmark Performance
                BenchmarkOee = benchmarkData.AverageOee,
                BenchmarkAvailability = benchmarkData.AverageAvailability,
                BenchmarkReliabilityScore = benchmarkData.AverageReliabilityScore,
                BenchmarkMtbf = benchmarkData.AverageMtbf,
                BenchmarkMttr = benchmarkData.AverageMttr,
                
                // Performance Gaps
                OeeGap = oeeAnalysis.OeeScore - benchmarkData.AverageOee,
                AvailabilityGap = reliabilityMetrics.Availability - benchmarkData.AverageAvailability,
                ReliabilityGap = reliabilityMetrics.ReliabilityScore - benchmarkData.AverageReliabilityScore,
                MtbfGap = reliabilityMetrics.MeanTimeBetweenFailures - benchmarkData.AverageMtbf,
                MttrGap = benchmarkData.AverageMttr - reliabilityMetrics.MeanTimeToRepair, // Lower is better
                
                // Rankings
                OeePercentileRank = CalculatePercentileRank(oeeAnalysis.OeeScore, benchmarkData.OeeDistribution),
                AvailabilityPercentileRank = CalculatePercentileRank(reliabilityMetrics.Availability, benchmarkData.AvailabilityDistribution),
                ReliabilityPercentileRank = CalculatePercentileRank(reliabilityMetrics.ReliabilityScore, benchmarkData.ReliabilityDistribution),
                
                OverallPerformanceRating = CalculateOverallPerformanceRating(oeeAnalysis.OeeScore, reliabilityMetrics.Availability, reliabilityMetrics.ReliabilityScore, benchmarkData),
                ImprovementRecommendations = await GenerateImprovementRecommendationsAsync(assetId, oeeAnalysis, reliabilityMetrics, benchmarkData)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance benchmark for asset {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Predictive Analytics Integration

    public async Task<AssetHealthTrendDto> GetAssetHealthTrendAsync(
        Guid assetId, int periodMonths = 12)
    {
        try
        {
            _logger.LogInformation("Getting asset health trend for asset {AssetId}", assetId);

            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                throw new ArgumentException($"Asset with ID {assetId} not found");

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddMonths(-periodMonths);

            // Get IoT health data if available
            var assetHealth = await _iotService.GetAssetHealthAsync(assetId);
            
            // Calculate historical performance metrics by month
            var monthlyMetrics = new List<MonthlyPerformanceMetricDto>();
            
            for (int i = 0; i < periodMonths; i++)
            {
                var monthEnd = endDate.AddMonths(-i);
                var monthStart = monthEnd.AddMonths(-1);
                
                var monthlyOee = await CalculateOeeAsync(assetId, monthStart, monthEnd);
                var monthlyReliability = await CalculateReliabilityMetricsAsync(assetId, monthStart, monthEnd);
                
                monthlyMetrics.Add(new MonthlyPerformanceMetricDto
                {
                    Month = monthEnd.ToString("yyyy-MM"),
                    Date = monthEnd,
                    OeeScore = monthlyOee.OeeScore,
                    Availability = monthlyReliability.Availability,
                    ReliabilityScore = monthlyReliability.ReliabilityScore,
                    FailureCount = monthlyReliability.TotalFailures,
                    MaintenanceCost = await GetMonthlyMaintenanceCostAsync(assetId, monthStart, monthEnd)
                });
            }

            monthlyMetrics = monthlyMetrics.OrderBy(m => m.Date).ToList();

            return new AssetHealthTrendDto
            {
                AssetId = assetId,
                AssetName = asset.Name,
                AnalysisPeriodMonths = periodMonths,
                CurrentHealthScore = assetHealth?.HealthScore ?? 75.0,
                HealthTrend = assetHealth?.HealthTrend ?? "Stable",
                MonthlyMetrics = monthlyMetrics,
                TrendAnalysis = AnalyzePerformanceTrends(monthlyMetrics),
                PredictiveInsights = await GeneratePredictiveInsightsAsync(assetId, monthlyMetrics),
                MaintenanceRecommendations = await GenerateMaintenanceRecommendationsAsync(assetId, monthlyMetrics, assetHealth?.HealthScore ?? 75.0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset health trend for asset {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Advanced KPI Calculations

    public async Task<AssetKpiDashboardDto> GetAssetKpiDashboardAsync(
        Guid? assetId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var end = endDate ?? DateTime.UtcNow;
            var start = startDate ?? end.AddMonths(-3);

            _logger.LogInformation("Getting asset KPI dashboard from {StartDate} to {EndDate}", start, end);

            var assets = assetId.HasValue 
                ? new[] { await _assetService.GetAssetByIdAsync(assetId.Value) }.Where(a => a != null).Cast<MaintenanceAssetDto>()
                : await _assetService.GetAllAssetsAsync();

            var kpiSummary = new AssetKpiSummaryDto();
            var assetMetrics = new List<AssetMetricSummaryDto>();

            foreach (var asset in assets)
            {
                var oee = await CalculateOeeAsync(asset.Id, start, end);
                var reliability = await CalculateReliabilityMetricsAsync(asset.Id, start, end);
                var cost = await CalculateTotalMaintenanceCostAsync(asset.Id, start, end);

                // Aggregate KPIs
                kpiSummary.TotalOeeScore += oee.OeeScore;
                kpiSummary.TotalAvailability += reliability.Availability;
                kpiSummary.TotalReliabilityScore += reliability.ReliabilityScore;
                kpiSummary.TotalMaintenanceCost += cost;
                kpiSummary.TotalFailures += reliability.TotalFailures;
                kpiSummary.TotalAssets++;

                assetMetrics.Add(new AssetMetricSummaryDto
                {
                    AssetId = asset.Id,
                    AssetName = asset.Name,
                    AssetType = asset.AssetType ?? "Unknown",
                    OeeScore = oee.OeeScore,
                    Availability = reliability.Availability,
                    ReliabilityScore = reliability.ReliabilityScore,
                    MaintenanceCost = cost,
                    FailureCount = reliability.TotalFailures,
                    LastMaintenanceDate = await GetLastMaintenanceDateAsync(asset.Id),
                    NextScheduledMaintenance = await GetNextScheduledMaintenanceAsync(asset.Id),
                    HealthStatus = DetermineHealthStatus(oee.OeeScore, reliability.ReliabilityScore)
                });
            }

            // Calculate averages
            if (kpiSummary.TotalAssets > 0)
            {
                kpiSummary.AverageOeeScore = kpiSummary.TotalOeeScore / kpiSummary.TotalAssets;
                kpiSummary.AverageAvailability = kpiSummary.TotalAvailability / kpiSummary.TotalAssets;
                kpiSummary.AverageReliabilityScore = kpiSummary.TotalReliabilityScore / kpiSummary.TotalAssets;
                kpiSummary.AverageMaintenanceCostPerAsset = kpiSummary.TotalMaintenanceCost / kpiSummary.TotalAssets;
            }

            return new AssetKpiDashboardDto
            {
                ReportDate = DateTime.UtcNow,
                AnalysisPeriodStart = start,
                AnalysisPeriodEnd = end,
                KpiSummary = kpiSummary,
                AssetMetrics = assetMetrics.OrderByDescending(a => a.OeeScore).ToList(),
                TopPerformers = assetMetrics.OrderByDescending(a => a.OeeScore).Take(5).ToList(),
                UnderPerformers = assetMetrics.Where(a => a.OeeScore < 60 || a.ReliabilityScore < 70).OrderBy(a => a.OeeScore).ToList(),
                CriticalAssets = assetMetrics.Where(a => a.HealthStatus == "Critical" || a.FailureCount > 5).OrderByDescending(a => a.FailureCount).ToList(),
                UpcomingMaintenance = assetMetrics.Where(a => a.NextScheduledMaintenance.HasValue && 
                                                            a.NextScheduledMaintenance.Value <= DateTime.UtcNow.AddDays(30))
                                                   .OrderBy(a => a.NextScheduledMaintenance).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset KPI dashboard");
            throw;
        }
    }

    public async Task<List<AssetPerformanceKpiDto>> CalculateAdvancedKpisAsync(
        DateTime startDate, DateTime endDate, string kpiCategory = "All")
    {
        try
        {
            _logger.LogInformation("Calculating advanced KPIs from {StartDate} to {EndDate}", startDate, endDate);

            var kpis = new List<AssetPerformanceKpiDto>();

            // Get all assets for fleet-level calculations
            var assets = await _assetService.GetAllAssetsAsync();

            // Calculate various KPI categories
            if (kpiCategory == "All" || kpiCategory == "Efficiency")
            {
                kpis.AddRange(await CalculateEfficiencyKpisAsync(assets, startDate, endDate));
            }

            if (kpiCategory == "All" || kpiCategory == "Reliability")
            {
                kpis.AddRange(await CalculateReliabilityKpisAsync(assets, startDate, endDate));
            }

            if (kpiCategory == "All" || kpiCategory == "Cost")
            {
                kpis.AddRange(await CalculateCostKpisAsync(assets, startDate, endDate));
            }

            if (kpiCategory == "All" || kpiCategory == "Quality")
            {
                kpis.AddRange(await CalculateQualityKpisAsync(assets, startDate, endDate));
            }

            if (kpiCategory == "All" || kpiCategory == "Sustainability")
            {
                kpis.AddRange(await CalculateSustainabilityKpisAsync(assets, startDate, endDate));
            }

            return kpis.OrderByDescending(k => k.ImportanceWeight).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating advanced KPIs");
            throw;
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<double> CalculateAvailabilityAsync(
        Guid assetId, DateTime startDate, DateTime endDate, IEnumerable<WorkOrderDto> workOrders)
    {
        var totalHours = (endDate - startDate).TotalHours;
        var downtime = workOrders.Where(wo => wo.Type == "Emergency" || wo.Type == "Corrective")
                                .Sum(wo => wo.ActualDuration?.TotalHours ?? 2.0); // Default 2 hours if not specified

        var plannedDowntime = await GetPlannedDowntimeHoursAsync(assetId, startDate, endDate);
        var availableHours = totalHours - plannedDowntime;
        
        return availableHours > 0 ? Math.Max(0, (availableHours - downtime) / availableHours * 100) : 100;
    }

    private async Task<double> CalculatePerformanceAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        // Mock performance calculation - would use actual production data
        return 85.0 + Random.Shared.NextDouble() * 10.0; // 85-95%
    }

    private async Task<double> CalculateQualityAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        // Mock quality calculation - would use actual quality data
        return 90.0 + Random.Shared.NextDouble() * 8.0; // 90-98%
    }

    private async Task<double> GetPlannedProductionTimeAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        return (endDate - startDate).TotalHours * 0.85; // Assuming 85% planned production time
    }

    private async Task<double> GetActualRunTimeAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var plannedTime = await GetPlannedProductionTimeAsync(assetId, startDate, endDate);
        return plannedTime * 0.92; // Assuming 92% actual run time
    }

    private async Task<double> GetIdealCycleTimeAsync(Guid assetId)
    {
        return 2.5; // Mock ideal cycle time in minutes
    }

    private async Task<double> GetActualCycleTimeAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var ideal = await GetIdealCycleTimeAsync(assetId);
        return ideal * (1.1 + Random.Shared.NextDouble() * 0.2); // 10-30% slower than ideal
    }

    private async Task<int> GetGoodUnitsAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var total = await GetTotalUnitsAsync(assetId, startDate, endDate);
        return (int)(total * (0.92 + Random.Shared.NextDouble() * 0.06)); // 92-98% good units
    }

    private async Task<int> GetTotalUnitsAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        // Mock total units calculation
        var days = (endDate - startDate).TotalDays;
        return (int)(days * (100 + Random.Shared.NextDouble() * 50)); // 100-150 units per day
    }

    private async Task<List<OeeLossDto>> CalculateOeeLossesAsync(
        Guid assetId, DateTime startDate, DateTime endDate, IEnumerable<WorkOrderDto> workOrders)
    {
        var losses = new List<OeeLossDto>();

        // Breakdown losses
        var breakdowns = workOrders.Where(wo => wo.Type == "Emergency").ToList();
        if (breakdowns.Any())
        {
            losses.Add(new OeeLossDto
            {
                LossCategory = "Availability",
                LossType = "Breakdown",
                Duration = breakdowns.Sum(b => b.ActualDuration?.TotalHours ?? 3.0),
                ImpactPercentage = 15.0,
                Reason = $"{breakdowns.Count} equipment breakdowns"
            });
        }

        // Setup and adjustment losses
        losses.Add(new OeeLossDto
        {
            LossCategory = "Performance",
            LossType = "Setup/Changeover",
            Duration = 8.0,
            ImpactPercentage = 5.0,
            Reason = "Setup and changeover time"
        });

        return losses;
    }

    private string DeterminePerformanceCategory(double oeeScore)
    {
        return oeeScore switch
        {
            >= 85 => "World Class",
            >= 65 => "Good",
            >= 40 => "Average",
            _ => "Poor"
        };
    }

    private async Task<double> CalculateMeanTimeBetweenFailuresAsync(Guid assetId, List<WorkOrderDto> failures)
    {
        if (failures.Count <= 1) return 8760; // Default 1 year in hours

        failures = failures.OrderBy(f => f.CreatedAt).ToList();
        var totalTimeBetweenFailures = 0.0;

        for (int i = 1; i < failures.Count; i++)
        {
            totalTimeBetweenFailures += (failures[i].CreatedAt - failures[i-1].CreatedAt).TotalHours;
        }

        return totalTimeBetweenFailures / (failures.Count - 1);
    }

    private async Task<double> CalculateMeanTimeToRepairAsync(Guid assetId, List<WorkOrderDto> failures)
    {
        if (!failures.Any()) return 0;

        var totalRepairTime = failures.Sum(f => f.ActualDuration?.TotalHours ?? 3.0);
        return totalRepairTime / failures.Count;
    }

    private async Task<double> CalculateMeanTimeToFailureAsync(Guid assetId, List<WorkOrderDto> failures)
    {
        // MTTF calculation for non-repairable systems or first failure
        return await CalculateMeanTimeBetweenFailuresAsync(assetId, failures);
    }

    private async Task<double> CalculateReliabilityAvailabilityAsync(double mtbf, double mttr)
    {
        return mtbf / (mtbf + mttr) * 100.0;
    }

    private double CalculateReliabilityScore(double mtbf, double mttr, double availability)
    {
        // Composite reliability score combining MTBF, MTTR, and availability
        var mtbfScore = Math.Min(100, mtbf / 8760 * 100); // Normalize to yearly MTBF
        var mttrScore = Math.Max(0, 100 - (mttr / 24 * 10)); // Penalty for longer repair times
        var availabilityScore = availability;

        return (mtbfScore * 0.4 + mttrScore * 0.3 + availabilityScore * 0.3);
    }

    private async Task<double> GetPlannedDowntimeHoursAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var schedules = await _scheduleService.GetSchedulesByAssetAsync(assetId, startDate, endDate);
        return schedules.Where(s => s.MaintenanceType == "Preventive")
                       .Sum(s => (s.ScheduledEndTime - s.ScheduledStartTime).TotalHours);
    }

    private async Task<double> GetUnplannedDowntimeHoursAsync(Guid assetId, List<WorkOrderDto> failures)
    {
        return failures.Sum(f => f.ActualDuration?.TotalHours ?? 3.0);
    }

    private async Task<double> GetOperatingHoursAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var totalHours = (endDate - startDate).TotalHours;
        var plannedDowntime = await GetPlannedDowntimeHoursAsync(assetId, startDate, endDate);
        return totalHours - plannedDowntime;
    }

    private async Task<List<FailureModeAnalysisDto>> AnalyzeFailureModesAsync(Guid assetId, List<WorkOrderDto> failures)
    {
        var failureModes = failures.GroupBy(f => f.Description.Contains("electrical", StringComparison.OrdinalIgnoreCase) ? "Electrical" :
                                              f.Description.Contains("mechanical", StringComparison.OrdinalIgnoreCase) ? "Mechanical" :
                                              f.Description.Contains("hydraulic", StringComparison.OrdinalIgnoreCase) ? "Hydraulic" : "Other")
                                  .Select(g => new FailureModeAnalysisDto
                                  {
                                      FailureMode = g.Key,
                                      Frequency = g.Count(),
                                      Percentage = (double)g.Count() / failures.Count * 100,
                                      AverageRepairTime = g.Average(f => f.ActualDuration?.TotalHours ?? 3.0),
                                      TotalCost = g.Sum(f => GetWorkOrderCost(f))
                                  })
                                  .OrderByDescending(f => f.Frequency)
                                  .ToList();

        return failureModes;
    }

    private async Task<ReliabilityTrendAnalysisDto> PerformReliabilityTrendAnalysisAsync(Guid assetId, IEnumerable<WorkOrderDto> workOrders)
    {
        // Simple trend analysis
        return new ReliabilityTrendAnalysisDto
        {
            TrendDirection = "Stable",
            TrendStrength = 0.25,
            FailureFrequencyTrend = "Decreasing",
            RepairTimeTrend = "Stable",
            RecommendedActions = new List<string>
            {
                "Continue current maintenance practices",
                "Monitor for any changes in failure patterns"
            }
        };
    }

    private decimal GetWorkOrderCost(WorkOrderDto workOrder)
    {
        // Mock cost calculation - would use actual cost data
        return workOrder.Priority switch
        {
            "Critical" => 5000m,
            "High" => 2500m,
            "Medium" => 1200m,
            _ => 600m
        };
    }

    private string DetermineReliabilityCategory(double reliabilityScore)
    {
        return reliabilityScore switch
        {
            >= 90 => "Excellent",
            >= 80 => "Good",
            >= 70 => "Fair",
            _ => "Poor"
        };
    }

    // Mock benchmark data method
    private async Task<BenchmarkDataDto> GetBenchmarkDataAsync(string assetType, string category)
    {
        return new BenchmarkDataDto
        {
            AverageOee = 75.0,
            AverageAvailability = 85.0,
            AverageReliabilityScore = 80.0,
            AverageMtbf = 2160.0, // 90 days
            AverageMttr = 4.0,
            OeeDistribution = new[] { 50.0, 65.0, 75.0, 82.0, 90.0 },
            AvailabilityDistribution = new[] { 70.0, 80.0, 85.0, 90.0, 95.0 },
            ReliabilityDistribution = new[] { 60.0, 72.0, 80.0, 87.0, 94.0 }
        };
    }

    private double CalculatePercentileRank(double value, double[] distribution)
    {
        if (distribution == null || !distribution.Any()) return 50.0;

        var sorted = distribution.OrderBy(x => x).ToArray();
        var rank = Array.BinarySearch(sorted, value);
        
        if (rank >= 0)
        {
            return (double)rank / (sorted.Length - 1) * 100;
        }
        else
        {
            var insertPoint = ~rank;
            return (double)insertPoint / sorted.Length * 100;
        }
    }

    private string CalculateOverallPerformanceRating(double oee, double availability, double reliability, BenchmarkDataDto benchmark)
    {
        var avgPerformance = (oee + availability + reliability) / 3.0;
        var benchmarkAvg = (benchmark.AverageOee + benchmark.AverageAvailability + benchmark.AverageReliabilityScore) / 3.0;

        var gap = avgPerformance - benchmarkAvg;
        
        return gap switch
        {
            >= 10 => "Excellent",
            >= 5 => "Above Average",
            >= -5 => "Average",
            _ => "Below Average"
        };
    }

    private async Task<List<string>> GenerateImprovementRecommendationsAsync(
        Guid assetId, OeeAnalysisDto oee, AssetReliabilityMetricsDto reliability, BenchmarkDataDto benchmark)
    {
        var recommendations = new List<string>();

        if (oee.OeeScore < benchmark.AverageOee)
        {
            recommendations.Add($"Focus on improving OEE - currently {oee.OeeScore:F1}% vs industry average {benchmark.AverageOee:F1}%");
        }

        if (reliability.MeanTimeToRepair > benchmark.AverageMttr)
        {
            recommendations.Add("Reduce repair times through better technician training and parts availability");
        }

        if (reliability.MeanTimeBetweenFailures < benchmark.AverageMtbf)
        {
            recommendations.Add("Implement more frequent preventive maintenance to reduce failure frequency");
        }

        return recommendations;
    }

    private PerformanceTrendAnalysisDto AnalyzePerformanceTrends(List<MonthlyPerformanceMetricDto> monthlyMetrics)
    {
        if (monthlyMetrics.Count < 3) return new PerformanceTrendAnalysisDto { TrendDirection = "Insufficient Data" };

        var recent = monthlyMetrics.TakeLast(3).Average(m => m.OeeScore);
        var earlier = monthlyMetrics.Take(3).Average(m => m.OeeScore);
        
        var trendDirection = recent > earlier + 2 ? "Improving" :
                            recent < earlier - 2 ? "Declining" : "Stable";

        return new PerformanceTrendAnalysisDto
        {
            TrendDirection = trendDirection,
            TrendStrength = Math.Abs(recent - earlier) / earlier,
            MonthlyChange = recent - earlier
        };
    }

    private async Task<List<string>> GeneratePredictiveInsightsAsync(Guid assetId, List<MonthlyPerformanceMetricDto> monthlyMetrics)
    {
        var insights = new List<string>();

        var latestMetric = monthlyMetrics.LastOrDefault();
        if (latestMetric?.OeeScore < 60)
        {
            insights.Add("Asset performance is below acceptable levels - immediate attention required");
        }

        if (monthlyMetrics.Count >= 3)
        {
            var trend = monthlyMetrics.TakeLast(3).Select(m => m.FailureCount).Sum();
            if (trend > 5)
            {
                insights.Add("Increasing failure trend detected - consider upgrading maintenance schedule");
            }
        }

        return insights;
    }

    private async Task<List<string>> GenerateMaintenanceRecommendationsAsync(
        Guid assetId, List<MonthlyPerformanceMetricDto> monthlyMetrics, double healthScore)
    {
        var recommendations = new List<string>();

        if (healthScore < 70)
        {
            recommendations.Add("Schedule comprehensive inspection and maintenance");
        }

        var recentFailures = monthlyMetrics.TakeLast(3).Sum(m => m.FailureCount);
        if (recentFailures > 3)
        {
            recommendations.Add("Implement more frequent preventive maintenance checks");
        }

        return recommendations;
    }

    private async Task<DateTime?> GetLastMaintenanceDateAsync(Guid assetId)
    {
        var workOrders = await _workOrderService.GetWorkOrdersByAssetAsync(assetId);
        return workOrders.Where(wo => wo.Status == "Completed")
                        .OrderByDescending(wo => wo.CompletedDate ?? wo.CreatedAt)
                        .FirstOrDefault()?.CompletedDate;
    }

    private async Task<DateTime?> GetNextScheduledMaintenanceAsync(Guid assetId)
    {
        var schedules = await _scheduleService.GetUpcomingSchedulesAsync(assetId, 90);
        return schedules.FirstOrDefault()?.ScheduledStartTime;
    }

    private string DetermineHealthStatus(double oeeScore, double reliabilityScore)
    {
        var avgScore = (oeeScore + reliabilityScore) / 2;
        return avgScore switch
        {
            >= 85 => "Excellent",
            >= 75 => "Good",
            >= 60 => "Fair",
            >= 40 => "Poor",
            _ => "Critical"
        };
    }

    private async Task<decimal> GetMonthlyMaintenanceCostAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var workOrders = await _workOrderService.GetWorkOrdersByAssetAsync(assetId);
        return workOrders.Where(wo => wo.CreatedAt >= startDate && wo.CreatedAt <= endDate)
                        .Sum(wo => GetWorkOrderCost(wo));
    }

    private async Task<decimal> CalculateTotalMaintenanceCostAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        return await GetMonthlyMaintenanceCostAsync(assetId, startDate, endDate);
    }

    // KPI calculation methods
    private async Task<List<AssetPerformanceKpiDto>> CalculateEfficiencyKpisAsync(IEnumerable<MaintenanceAssetDto> assets, DateTime startDate, DateTime endDate)
    {
        var kpis = new List<AssetPerformanceKpiDto>();
        
        var oeeScores = new List<double>();
        foreach (var asset in assets)
        {
            var oee = await CalculateOeeAsync(asset.Id, startDate, endDate);
            oeeScores.Add(oee.OeeScore);
        }

        kpis.Add(new AssetPerformanceKpiDto
        {
            KpiName = "Fleet Average OEE",
            KpiValue = oeeScores.Average(),
            KpiUnit = "%",
            KpiCategory = "Efficiency",
            TargetValue = 75.0,
            ImportanceWeight = 0.9,
            TrendDirection = "Stable",
            LastUpdated = DateTime.UtcNow
        });

        return kpis;
    }

    private async Task<List<AssetPerformanceKpiDto>> CalculateReliabilityKpisAsync(IEnumerable<MaintenanceAssetDto> assets, DateTime startDate, DateTime endDate)
    {
        // Implementation for reliability KPIs
        return new List<AssetPerformanceKpiDto>();
    }

    private async Task<List<AssetPerformanceKpiDto>> CalculateCostKpisAsync(IEnumerable<MaintenanceAssetDto> assets, DateTime startDate, DateTime endDate)
    {
        // Implementation for cost KPIs
        return new List<AssetPerformanceKpiDto>();
    }

    private async Task<List<AssetPerformanceKpiDto>> CalculateQualityKpisAsync(IEnumerable<MaintenanceAssetDto> assets, DateTime startDate, DateTime endDate)
    {
        // Implementation for quality KPIs
        return new List<AssetPerformanceKpiDto>();
    }

    private async Task<List<AssetPerformanceKpiDto>> CalculateSustainabilityKpisAsync(IEnumerable<MaintenanceAssetDto> assets, DateTime startDate, DateTime endDate)
    {
        // Implementation for sustainability KPIs
        return new List<AssetPerformanceKpiDto>();
    }

    #endregion
}

#region Supporting DTOs

public class AssetReliabilityMetricsDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public double MeanTimeBetweenFailures { get; set; } // Hours
    public double MeanTimeToRepair { get; set; } // Hours
    public double MeanTimeToFailure { get; set; } // Hours
    public double Availability { get; set; } // Percentage
    public double ReliabilityScore { get; set; } // Composite score
    public double FailureRate { get; set; } // Failures per year
    public int TotalFailures { get; set; }
    public double PlannedDowntimeHours { get; set; }
    public double UnplannedDowntimeHours { get; set; }
    public double OperatingHours { get; set; }
    public List<FailureModeAnalysisDto> FailureModes { get; set; } = new();
    public ReliabilityTrendAnalysisDto TrendAnalysis { get; set; } = null!;
}

public class AssetReliabilityRankingDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public double ReliabilityScore { get; set; }
    public double Availability { get; set; }
    public double MeanTimeBetweenFailures { get; set; }
    public double MeanTimeToRepair { get; set; }
    public int TotalFailures { get; set; }
    public double FailureRate { get; set; }
    public string PerformanceCategory { get; set; } = string.Empty;
}

public class FailureModeAnalysisDto
{
    public string FailureMode { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public double Percentage { get; set; }
    public double AverageRepairTime { get; set; }
    public decimal TotalCost { get; set; }
}

public class ReliabilityTrendAnalysisDto
{
    public string TrendDirection { get; set; } = string.Empty;
    public double TrendStrength { get; set; }
    public string FailureFrequencyTrend { get; set; } = string.Empty;
    public string RepairTimeTrend { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
}

public class AssetPerformanceBenchmarkDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string BenchmarkCategory { get; set; } = string.Empty;
    public DateTime BenchmarkDate { get; set; }

    // Current Performance
    public double CurrentOee { get; set; }
    public double CurrentAvailability { get; set; }
    public double CurrentReliabilityScore { get; set; }
    public double CurrentMtbf { get; set; }
    public double CurrentMttr { get; set; }

    // Benchmark Performance
    public double BenchmarkOee { get; set; }
    public double BenchmarkAvailability { get; set; }
    public double BenchmarkReliabilityScore { get; set; }
    public double BenchmarkMtbf { get; set; }
    public double BenchmarkMttr { get; set; }

    // Performance Gaps
    public double OeeGap { get; set; }
    public double AvailabilityGap { get; set; }
    public double ReliabilityGap { get; set; }
    public double MtbfGap { get; set; }
    public double MttrGap { get; set; }

    // Rankings
    public double OeePercentileRank { get; set; }
    public double AvailabilityPercentileRank { get; set; }
    public double ReliabilityPercentileRank { get; set; }

    public string OverallPerformanceRating { get; set; } = string.Empty;
    public List<string> ImprovementRecommendations { get; set; } = new();
}

public class BenchmarkDataDto
{
    public double AverageOee { get; set; }
    public double AverageAvailability { get; set; }
    public double AverageReliabilityScore { get; set; }
    public double AverageMtbf { get; set; }
    public double AverageMttr { get; set; }
    public double[] OeeDistribution { get; set; } = Array.Empty<double>();
    public double[] AvailabilityDistribution { get; set; } = Array.Empty<double>();
    public double[] ReliabilityDistribution { get; set; } = Array.Empty<double>();
}

public class AssetHealthTrendDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int AnalysisPeriodMonths { get; set; }
    public double CurrentHealthScore { get; set; }
    public string HealthTrend { get; set; } = string.Empty;
    public List<MonthlyPerformanceMetricDto> MonthlyMetrics { get; set; } = new();
    public PerformanceTrendAnalysisDto TrendAnalysis { get; set; } = null!;
    public List<string> PredictiveInsights { get; set; } = new();
    public List<string> MaintenanceRecommendations { get; set; } = new();
}

public class MonthlyPerformanceMetricDto
{
    public string Month { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double OeeScore { get; set; }
    public double Availability { get; set; }
    public double ReliabilityScore { get; set; }
    public int FailureCount { get; set; }
    public decimal MaintenanceCost { get; set; }
}

public class PerformanceTrendAnalysisDto
{
    public string TrendDirection { get; set; } = string.Empty;
    public double TrendStrength { get; set; }
    public double MonthlyChange { get; set; }
}

public class AssetKpiDashboardDto
{
    public DateTime ReportDate { get; set; }
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public AssetKpiSummaryDto KpiSummary { get; set; } = null!;
    public List<AssetMetricSummaryDto> AssetMetrics { get; set; } = new();
    public List<AssetMetricSummaryDto> TopPerformers { get; set; } = new();
    public List<AssetMetricSummaryDto> UnderPerformers { get; set; } = new();
    public List<AssetMetricSummaryDto> CriticalAssets { get; set; } = new();
    public List<AssetMetricSummaryDto> UpcomingMaintenance { get; set; } = new();
}

public class AssetKpiSummaryDto
{
    public int TotalAssets { get; set; }
    public double TotalOeeScore { get; set; }
    public double AverageOeeScore { get; set; }
    public double TotalAvailability { get; set; }
    public double AverageAvailability { get; set; }
    public double TotalReliabilityScore { get; set; }
    public double AverageReliabilityScore { get; set; }
    public decimal TotalMaintenanceCost { get; set; }
    public decimal AverageMaintenanceCostPerAsset { get; set; }
    public int TotalFailures { get; set; }
}

public class AssetMetricSummaryDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public double OeeScore { get; set; }
    public double Availability { get; set; }
    public double ReliabilityScore { get; set; }
    public decimal MaintenanceCost { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextScheduledMaintenance { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
}

public class AssetPerformanceKpiDto
{
    public string KpiName { get; set; } = string.Empty;
    public double KpiValue { get; set; }
    public string KpiUnit { get; set; } = string.Empty;
    public string KpiCategory { get; set; } = string.Empty;
    public double TargetValue { get; set; }
    public double ImportanceWeight { get; set; }
    public string TrendDirection { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

#endregion