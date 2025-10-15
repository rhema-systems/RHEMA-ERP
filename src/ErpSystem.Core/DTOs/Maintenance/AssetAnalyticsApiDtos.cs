namespace ErpSystem.Core.DTOs.Maintenance;

#region Request DTOs

/// <summary>
/// Base request DTO for asset analytics with date range filtering
/// </summary>
public class AssetAnalyticsBaseRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
}

/// <summary>
/// Request for OEE analysis
/// </summary>
public class OeeAnalysisRequest : AssetAnalyticsBaseRequest
{
    /// <summary>
    /// Optional asset type filter
    /// </summary>
    public string? AssetType { get; set; }
    
    /// <summary>
    /// Include detailed breakdown components
    /// </summary>
    public bool IncludeBreakdown { get; set; } = true;
}

/// <summary>
/// Request for fleet OEE analysis
/// </summary>
public class FleetOeeAnalysisRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? AssetType { get; set; }
    public int? TopCount { get; set; } = 10;
}

/// <summary>
/// Request for asset reliability metrics
/// </summary>
public class ReliabilityMetricsRequest : AssetAnalyticsBaseRequest
{
    /// <summary>
    /// Include failure mode analysis
    /// </summary>
    public bool IncludeFailureModes { get; set; } = true;
    
    /// <summary>
    /// Include trend analysis
    /// </summary>
    public bool IncludeTrends { get; set; } = true;
}

/// <summary>
/// Request for asset reliability rankings
/// </summary>
public class ReliabilityRankingsRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TopCount { get; set; } = 10;
    public string? AssetType { get; set; }
    public string OrderBy { get; set; } = "ReliabilityScore"; // ReliabilityScore, MTBF, MTTR, Availability
}

/// <summary>
/// Request for performance benchmarking
/// </summary>
public class PerformanceBenchmarkRequest
{
    public Guid AssetId { get; set; }
    public string BenchmarkCategory { get; set; } = "Industry"; // Industry, WorldClass, Fleet
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Request for asset performance comparison
/// </summary>
public class AssetComparisonRequest
{
    public Guid AssetId { get; set; }
    public string IndustryType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Request for asset performance ranking
/// </summary>
public class PerformanceRankingRequest
{
    public string MetricType { get; set; } = "OEE"; // OEE, Availability, MTBF, MTTR, Cost
    public int TopCount { get; set; } = 10;
    public int BottomCount { get; set; } = 5;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? AssetType { get; set; }
}

/// <summary>
/// Request for asset health trend analysis
/// </summary>
public class AssetHealthTrendRequest
{
    public Guid AssetId { get; set; }
    public int PeriodMonths { get; set; } = 12;
    public bool IncludePredictions { get; set; } = true;
}

/// <summary>
/// Request for asset performance prediction
/// </summary>
public class AssetPerformancePredictionRequest
{
    public Guid AssetId { get; set; }
    public int PredictionDays { get; set; } = 30;
    public string[] MetricTypes { get; set; } = { "OEE", "Availability", "MaintenanceCost" };
}

/// <summary>
/// Request for KPI dashboard
/// </summary>
public class AssetKpiDashboardRequest
{
    public Guid? AssetId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string[] KpiCategories { get; set; } = { "All" }; // All, Efficiency, Reliability, Cost, Quality
}

/// <summary>
/// Request for advanced KPI calculations
/// </summary>
public class AdvancedKpiCalculationRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string KpiCategory { get; set; } = "All"; // All, Efficiency, Reliability, Cost, Quality, Sustainability
    public Guid? AssetId { get; set; }
    public string? AssetType { get; set; }
}

/// <summary>
/// Request for comprehensive performance metrics
/// </summary>
public class PerformanceMetricsRequest : AssetAnalyticsBaseRequest
{
    /// <summary>
    /// Include OEE breakdown
    /// </summary>
    public bool IncludeOeeBreakdown { get; set; } = true;
    
    /// <summary>
    /// Include reliability metrics
    /// </summary>
    public bool IncludeReliabilityMetrics { get; set; } = true;
    
    /// <summary>
    /// Include cost metrics
    /// </summary>
    public bool IncludeCostMetrics { get; set; } = true;
    
    /// <summary>
    /// Include quality metrics
    /// </summary>
    public bool IncludeQualityMetrics { get; set; } = true;
}

/// <summary>
/// Request for root cause analysis
/// </summary>
public class RootCauseAnalysisRequest
{
    public Guid AssetId { get; set; }
    public DateTime IncidentDate { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public bool IncludePreventiveActions { get; set; } = true;
    public bool IncludeCorrectiveActions { get; set; } = true;
}

/// <summary>
/// Request for asset criticality analysis
/// </summary>
public class AssetCriticalityAnalysisRequest
{
    public Guid AssetId { get; set; }
    public bool IncludeMaintenanceStrategy { get; set; } = true;
    public bool IncludeMonitoringRequirements { get; set; } = true;
}

/// <summary>
/// Request for optimization recommendations
/// </summary>
public class OptimizationRecommendationsRequest
{
    public Guid? AssetId { get; set; }
    public string[] RecommendationTypes { get; set; } = { "All" }; // All, Cost, Performance, Reliability, Energy
    public decimal? MinPotentialSavings { get; set; }
    public int MaxRecommendations { get; set; } = 20;
}

/// <summary>
/// Request for total cost of ownership analysis
/// </summary>
public class TotalCostOfOwnershipRequest : AssetAnalyticsBaseRequest
{
    public bool IncludeCostBreakdown { get; set; } = true;
    public bool IncludeAnnualizedCosts { get; set; } = true;
}

/// <summary>
/// Request for cost efficiency analysis
/// </summary>
public class CostEfficiencyAnalysisRequest : AssetAnalyticsBaseRequest
{
    public bool IncludeBenchmarkComparison { get; set; } = true;
    public bool IncludeImprovementOpportunities { get; set; } = true;
}

/// <summary>
/// Request for maintenance ROI analysis
/// </summary>
public class MaintenanceRoiAnalysisRequest : AssetAnalyticsBaseRequest
{
    public bool IncludePaybackPeriod { get; set; } = true;
    public bool IncludeNetPresentValue { get; set; } = true;
}

/// <summary>
/// Request for energy performance analysis
/// </summary>
public class EnergyPerformanceAnalysisRequest : AssetAnalyticsBaseRequest
{
    public bool IncludeEfficiencyRating { get; set; } = true;
    public bool IncludeCarbonFootprint { get; set; } = true;
    public bool IncludeOptimizationRecommendations { get; set; } = true;
}

/// <summary>
/// Request for environmental impact analysis
/// </summary>
public class EnvironmentalImpactAnalysisRequest : AssetAnalyticsBaseRequest
{
    public bool IncludeComplianceStatus { get; set; } = true;
    public bool IncludeImprovementRecommendations { get; set; } = true;
    public bool IncludeRegulatoryRisk { get; set; } = true;
}

/// <summary>
/// Asset performance metrics DTO
/// </summary>
public class AssetPerformanceMetricsDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public double Oee { get; set; }
    public double Availability { get; set; }
    public double Performance { get; set; }
    public double Quality { get; set; }
    public double ReliabilityScore { get; set; }
    public decimal MaintenanceCosts { get; set; }
    public int FailureCount { get; set; }
    public double UtilizationRate { get; set; }
}

/// <summary>
/// Asset reliability metrics DTO
/// </summary>
public class AssetReliabilityMetricsDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public double MeanTimeBetweenFailures { get; set; }
    public double MeanTimeToRepair { get; set; }
    public double AvailabilityRate { get; set; }
    public double ReliabilityScore { get; set; }
    public int TotalFailures { get; set; }
    public double TotalDowntimeHours { get; set; }
    public List<FailureAnalysisDto> FailureBreakdown { get; set; } = new();
}

/// <summary>
/// Asset reliability ranking DTO
/// </summary>
public class AssetReliabilityRankingDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int Rank { get; set; }
    public double ReliabilityScore { get; set; }
    public double MeanTimeBetweenFailures { get; set; }
    public double AvailabilityRate { get; set; }
    public string PerformanceCategory { get; set; } = string.Empty;
}

/// <summary>
/// Asset performance benchmark DTO
/// </summary>
public class AssetPerformanceBenchmarkDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string BenchmarkCategory { get; set; } = string.Empty;
    public Dictionary<string, BenchmarkMetricDto> Metrics { get; set; } = new();
    public string OverallRating { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public List<string> ImprovementAreas { get; set; } = new();
}

/// <summary>
/// Asset benchmark comparison DTO
/// </summary>
public class AssetBenchmarkComparisonDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public Dictionary<string, ComparisonMetricDto> Comparisons { get; set; } = new();
    public string OverallPerformance { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
    
    // Additional properties for service compatibility
    public string IndustryType { get; set; } = string.Empty;
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
}

/// <summary>
/// Asset performance ranking DTO
/// </summary>
public class AssetPerformanceRankingDto
{
    public List<AssetRankingItemDto> Rankings { get; set; } = new();
    public string MetricType { get; set; } = string.Empty;
    public DateTime RankingDate { get; set; }
    public int TotalAssets { get; set; }
}

/// <summary>
/// Individual asset ranking item
/// </summary>
public class AssetRankingItemDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int Rank { get; set; }
    public double Score { get; set; }
    public string PerformanceCategory { get; set; } = string.Empty;
}

/// <summary>
/// Asset health trend DTO
/// </summary>
public class AssetHealthTrendDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public List<AssetHealthDataPointDto> HealthData { get; set; } = new();
    public string OverallTrend { get; set; } = string.Empty;
    public double CurrentHealthScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> HealthFactors { get; set; } = new();
}

/// <summary>
/// Asset KPI dashboard DTO
/// </summary>
public class AssetKpiDashboardDto
{
    public Guid TenantId { get; set; }
    public DateTime DashboardDate { get; set; }
    public List<AssetPerformanceKpiDto> Kpis { get; set; } = new();
    public AssetKpiSummaryDto Summary { get; set; } = new();
}

/// <summary>
/// Asset performance KPI DTO
/// </summary>
public class AssetPerformanceKpiDto
{
    public string KpiName { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double TargetValue { get; set; }
    public double PreviousValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string TrendDirection { get; set; } = string.Empty;
    public string PerformanceStatus { get; set; } = string.Empty;
    public List<TrendDataPointDto> TrendData { get; set; } = new();
}

/// <summary>
/// Asset performance dashboard DTO
/// </summary>
public class AssetPerformanceDashboardDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime DashboardDate { get; set; }
    public AssetPerformanceMetricsDto Metrics { get; set; } = new();
    public List<AssetPerformanceKpiDto> Kpis { get; set; } = new();
    public List<PerformanceAlertDto> Alerts { get; set; } = new();
    public PerformanceTrendsDto Trends { get; set; } = new();
}

/// <summary>
/// Asset root cause analysis DTO
/// </summary>
public class AssetRootCauseAnalysisDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public string FailureType { get; set; } = string.Empty;
    public List<RootCauseFactorDto> RootCauses { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public double ConfidenceScore { get; set; }
    
    // Additional property for service compatibility
    public DateTime IncidentDate { get; set; }
}

/// <summary>
/// Asset criticality analysis DTO
/// </summary>
public class AssetCriticalityAnalysisDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public double CriticalityScore { get; set; }
    public string CriticalityLevel { get; set; } = string.Empty;
    public Dictionary<string, double> CriticalityFactors { get; set; } = new();
    public List<string> BusinessImpacts { get; set; } = new();
    public List<string> MitigationStrategies { get; set; } = new();
}

/// <summary>
/// Asset total cost of ownership DTO
/// </summary>
public class AssetTotalCostOfOwnershipDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal OperationalCost { get; set; }
    public decimal MaintenanceCost { get; set; }
    public decimal DisposalCost { get; set; }
    public Dictionary<string, decimal> CostBreakdown { get; set; } = new();
}

/// <summary>
/// Asset cost efficiency DTO
/// </summary>
public class AssetCostEfficiencyDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public double EfficiencyRatio { get; set; }
    public decimal CostPerUnit { get; set; }
    public decimal CostPerHour { get; set; }
    public string EfficiencyRating { get; set; } = string.Empty;
    public List<string> CostOptimizationAreas { get; set; } = new();
    public decimal PotentialSavings { get; set; }
    
    // Additional properties for service compatibility
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
}

/// <summary>
/// Maintenance return on investment DTO
/// </summary>
public class MaintenanceReturnOnInvestmentDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public decimal MaintenanceInvestment { get; set; }
    public decimal GeneratedSavings { get; set; }
    public double ROIPercentage { get; set; }
    public int PaybackPeriodMonths { get; set; }
    public List<ROIContributionDto> Contributions { get; set; } = new();
    
    // Additional properties for service compatibility
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
}

/// <summary>
/// Asset energy performance DTO
/// </summary>
public class AssetEnergyPerformanceDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public double EnergyConsumption { get; set; }
    public double EnergyEfficiency { get; set; }
    public decimal EnergyCost { get; set; }
    public double CarbonFootprint { get; set; }
    public List<EnergyMetricDto> EnergyMetrics { get; set; } = new();
}

/// <summary>
/// Asset environmental impact DTO
/// </summary>
public class AssetEnvironmentalImpactDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public double CarbonEmissions { get; set; }
    public double WaterUsage { get; set; }
    public double WasteGeneration { get; set; }
    public double EnvironmentalScore { get; set; }
    public List<EnvironmentalMetricDto> Metrics { get; set; } = new();
    public List<string> ImprovementRecommendations { get; set; } = new();
    
    // Additional properties for service compatibility
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
}

// Supporting DTOs

/// <summary>
/// Failure analysis DTO
/// </summary>
public class FailureAnalysisDto
{
    public string FailureType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double PercentageOfTotal { get; set; }
    public double AverageDowntime { get; set; }
}

/// <summary>
/// Comparison metric DTO
/// </summary>
public class ComparisonMetricDto
{
    public string MetricName { get; set; } = string.Empty;
    public double ActualValue { get; set; }
    public double BenchmarkValue { get; set; }
    public double PerformanceGap { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Asset KPI summary DTO
/// </summary>
public class AssetKpiSummaryDto
{
    public int TotalKpis { get; set; }
    public int OnTargetKpis { get; set; }
    public int BelowTargetKpis { get; set; }
    public double AveragePerformance { get; set; }
}

/// <summary>
/// ROI contribution DTO
/// </summary>
public class ROIContributionDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Energy metric DTO
/// </summary>
public class EnergyMetricDto
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Environmental metric DTO
/// </summary>
public class EnvironmentalMetricDto
{
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ImpactLevel { get; set; } = string.Empty;
}


#endregion

#region Analytics DTOs

/// <summary>
/// Maintenance alert DTO
/// </summary>
public class MaintenanceAlertDto
{
    public Guid Id { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsAcknowledged { get; set; }
    public Guid? AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    
    // Additional properties for service compatibility
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Top issue DTO
/// </summary>
public class TopIssueDto
{
    public string IssueType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double PercentageOfTotal { get; set; }
    public double AverageCost { get; set; }
    public double TotalCost { get; set; }
    public int AffectedAssets { get; set; }
    
    // Additional properties for service compatibility
    public string Issue { get; set; } = string.Empty;
    public int Frequency { get; set; }
}

/// <summary>
/// Monthly cost DTO
/// </summary>
public class MonthlyCostDto
{
    public DateTime Month { get; set; }
    public decimal TotalCost { get; set; }
    public decimal PreventiveCost { get; set; }
    public decimal CorrectiveCost { get; set; }
    public decimal EmergencyCost { get; set; }
    public int WorkOrderCount { get; set; }
}

/// <summary>
/// Work order metrics DTO
/// </summary>
public class WorkOrderMetricsDto
{
    public int TotalWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public int PendingWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public double CompletionRate { get; set; }
    public double AverageCompletionTime { get; set; }
    public decimal TotalCost { get; set; }
    public List<WorkOrderTypeMetricDto> TypeBreakdown { get; set; } = new();
}

/// <summary>
/// Work order type metric DTO
/// </summary>
public class WorkOrderTypeMetricDto
{
    public string WorkOrderType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public decimal AverageCost { get; set; }
    public double AverageCompletionTime { get; set; }
}

/// <summary>
/// Preventive maintenance report DTO
/// </summary>
public class PreventiveMaintenanceReportDto
{
    public DateTime ReportDate { get; set; }
    public int TotalPreventiveTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public double ComplianceRate { get; set; }
    public decimal TotalCost { get; set; }
    public List<AssetPreventiveMaintenanceDto> AssetBreakdown { get; set; } = new();
    public List<MaintenanceTypeBreakdownDto> TypeBreakdown { get; set; } = new();
}

/// <summary>
/// Asset preventive maintenance DTO
/// </summary>
public class AssetPreventiveMaintenanceDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int ScheduledTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public double ComplianceRate { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>
/// Maintenance type breakdown DTO
/// </summary>
public class MaintenanceTypeBreakdownDto
{
    public string MaintenanceType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public decimal AverageCost { get; set; }
    public double AverageCompletionTime { get; set; }
}

/// <summary>
/// Work order trends DTO
/// </summary>
public class WorkOrderTrendsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TrendDataPointDto> CreationTrend { get; set; } = new();
    public List<TrendDataPointDto> CompletionTrend { get; set; } = new();
    public List<TrendDataPointDto> CostTrend { get; set; } = new();
    public List<WorkOrderTypeMetricDto> TypeDistribution { get; set; } = new();
}

/// <summary>
/// Technician utilization report DTO
/// </summary>
public class TechnicianUtilizationReportDto
{
    public DateTime ReportDate { get; set; }
    public List<TechnicianUtilizationDto> TechnicianStats { get; set; } = new();
    public double OverallUtilizationRate { get; set; }
    public decimal TotalLaborCost { get; set; }
    public double AverageProductivity { get; set; }
}

/// <summary>
/// Technician utilization DTO
/// </summary>
public class TechnicianUtilizationDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public double UtilizationRate { get; set; }
    public int AssignedWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public double AverageCompletionTime { get; set; }
    public decimal TotalLaborCost { get; set; }
    public double ProductivityScore { get; set; }
}

/// <summary>
/// Asset reliability report DTO
/// </summary>
public class AssetReliabilityReportDto
{
    public DateTime ReportDate { get; set; }
    public double OverallReliability { get; set; }
    public List<AssetReliabilityDto> AssetReliabilities { get; set; } = new();
    public List<FailureModeAnalysisDto> FailureModes { get; set; } = new();
    public ReliabilityTrendDto ReliabilityTrend { get; set; } = new();
}

/// <summary>
/// Asset reliability DTO
/// </summary>
public class AssetReliabilityDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public double ReliabilityScore { get; set; }
    public double MTBF { get; set; }
    public double MTTR { get; set; }
    public double Availability { get; set; }
    public int FailureCount { get; set; }
    public double TotalDowntime { get; set; }
}

/// <summary>
/// Failure mode analysis DTO
/// </summary>
public class FailureModeAnalysisDto
{
    public string FailureMode { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public double Percentage { get; set; }
    public double AverageDowntime { get; set; }
    public decimal AverageCost { get; set; }
    public string Criticality { get; set; } = string.Empty;
}

/// <summary>
/// Reliability trend DTO
/// </summary>
public class ReliabilityTrendDto
{
    public List<TrendDataPointDto> ReliabilityTrend { get; set; } = new();
    public List<TrendDataPointDto> MTBFTrend { get; set; } = new();
    public List<TrendDataPointDto> MTTRTrend { get; set; } = new();
    public List<TrendDataPointDto> AvailabilityTrend { get; set; } = new();
}

#endregion

#region Performance Report DTOs

/// <summary>
/// Asset performance report DTO
/// </summary>
public class AssetPerformanceReportDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public int TotalWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public decimal TotalMaintenanceCost { get; set; }
    public double DowntimeHours { get; set; }
    public double UptimePercentage { get; set; }
    public double MeanTimeBetweenFailures { get; set; }
    public double MeanTimeToRepair { get; set; }
    public List<MaintenanceHistoryItemDto> MaintenanceHistory { get; set; } = new();
    
    // Additional properties for service compatibility
    public string ReportPeriod { get; set; } = string.Empty;
    public int PlannedWorkOrders { get; set; }
    public int UnplannedWorkOrders { get; set; }
    public double TotalDowntime { get; set; }
    public double AverageRepairTime { get; set; }
    public decimal MaintenanceCost { get; set; }
    public double LaborHours { get; set; }
    public double Availability { get; set; }
    public double Reliability { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Maintenance cost analysis DTO
/// </summary>
public class MaintenanceCostAnalysisDto
{
    public decimal TotalCost { get; set; }
    public decimal PreventiveCost { get; set; }
    public decimal CorrectiveCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal MaterialsCost { get; set; }
    public decimal ContractorCost { get; set; }
    public List<CostByAssetDto> CostByAsset { get; set; } = new();
    public List<CostByMonthDto> CostByMonth { get; set; } = new();
    public List<CostByCategoryDto> CostByCategory { get; set; } = new();
    
    // Additional properties for service compatibility
    public string ReportPeriod { get; set; } = string.Empty;
    public decimal TotalMaintenanceCost { get; set; }
    public decimal PartsCost { get; set; }
    public decimal PlannedMaintenanceCost { get; set; }
    public decimal UnplannedMaintenanceCost { get; set; }
    public List<TrendDataPointDto> CostTrend { get; set; } = new();
    public double BudgetVariance { get; set; }
    public double CostPerWorkOrder { get; set; }
}

/// <summary>
/// Upcoming maintenance DTO
/// </summary>
public class UpcomingMaintenanceDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public string Priority { get; set; } = string.Empty;
    public int DaysUntilDue { get; set; }
    
    // Additional property for service compatibility
    public DateTime DueDate { get; set; }
}

// Supporting DTOs

/// <summary>
/// Maintenance history item DTO
/// </summary>
public class MaintenanceHistoryItemDto
{
    public DateTime Date { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Status { get; set; } = string.Empty;
    public double Duration { get; set; }
}

/// <summary>
/// Cost by asset DTO
/// </summary>
public class CostByAssetDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Cost by month DTO
/// </summary>
public class CostByMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
}

/// <summary>
/// Cost by category DTO
/// </summary>
public class CostByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal Percentage { get; set; }
}

#endregion

#region Response DTOs

/// <summary>
/// Standard API response wrapper for all asset analytics endpoints
/// </summary>
/// <typeparam name="T">Response data type</typeparam>
public class AssetAnalyticsResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? Count { get; set; }
    public AssetAnalyticsMetadata? Metadata { get; set; }
}

/// <summary>
/// Metadata for analytics responses
/// </summary>
public class AssetAnalyticsMetadata
{
    public string? DataSource { get; set; }
    public DateTime? DataAsOf { get; set; }
    public string? CalculationMethod { get; set; }
    public int? SampleSize { get; set; }
    public double? ConfidenceLevel { get; set; }
}

/// <summary>
/// Paginated response for large result sets
/// </summary>
/// <typeparam name="T">Response data type</typeparam>
public class PaginatedAssetAnalyticsResponse<T> : AssetAnalyticsResponse<T>
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

/// <summary>
/// Response for OEE analysis with enhanced metrics
/// </summary>
public class OeeAnalysisResponse
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    // Core OEE Components
    public double Availability { get; set; }
    public double Performance { get; set; }
    public double Quality { get; set; }
    public double OeeScore { get; set; }
    
    // Performance Category
    public string PerformanceCategory { get; set; } = string.Empty; // WorldClass, Good, Fair, Poor
    
    // Benchmarks
    public double IndustryBenchmark { get; set; }
    public double WorldClassBenchmark { get; set; }
    
    // Loss Analysis
    public OeeLossAnalysisDto? LossAnalysis { get; set; }
    
    // Trend Information
    public double PreviousPeriodOee { get; set; }
    public double TrendChange { get; set; }
    public string TrendDirection { get; set; } = string.Empty;
    
    // Recommendations
    public List<string> ImprovementRecommendations { get; set; } = new();
}

/// <summary>
/// OEE Loss analysis breakdown
/// </summary>
public class OeeLossAnalysisDto
{
    public double AvailabilityLoss { get; set; }
    public double PerformanceLoss { get; set; }
    public double QualityLoss { get; set; }
    public List<LossCategoryDto> LossCategories { get; set; } = new();
}

/// <summary>
/// Individual loss category
/// </summary>
public class LossCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public double LossPercentage { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Response for asset performance dashboard
/// </summary>
public class AssetPerformanceDashboardResponse
{
    public Guid TenantId { get; set; }
    public DateTime DashboardDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    
    // Fleet Summary
    public FleetSummaryDto FleetSummary { get; set; } = new();
    
    // Asset Metrics
    public List<AssetPerformanceMetricsDto> AssetMetrics { get; set; } = new();
    
    // Top/Bottom Performers
    public List<AssetPerformanceMetricsDto> TopPerformers { get; set; } = new();
    public List<AssetPerformanceMetricsDto> BottomPerformers { get; set; } = new();
    
    // Critical Alerts
    public List<PerformanceAlertDto> CriticalAlerts { get; set; } = new();
    
    // Trends
    public PerformanceTrendsDto Trends { get; set; } = new();
}

/// <summary>
/// Fleet summary metrics
/// </summary>
public class FleetSummaryDto
{
    public int TotalAssets { get; set; }
    public double AverageOee { get; set; }
    public double AverageAvailability { get; set; }
    public double AverageReliabilityScore { get; set; }
    public decimal TotalMaintenanceCosts { get; set; }
    public int TotalFailures { get; set; }
    public double FleetUtilization { get; set; }
}

/// <summary>
/// Performance trends summary
/// </summary>
public class PerformanceTrendsDto
{
    public string OeeTrend { get; set; } = string.Empty;
    public string AvailabilityTrend { get; set; } = string.Empty;
    public string ReliabilityTrend { get; set; } = string.Empty;
    public string CostTrend { get; set; } = string.Empty;
    public List<TrendDataPointDto> MonthlyTrends { get; set; } = new();
}


/// <summary>
/// Performance alert DTO
/// </summary>
public class PerformanceAlertDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Enhanced benchmark comparison response
/// </summary>
public class BenchmarkComparisonResponse
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string BenchmarkCategory { get; set; } = string.Empty;
    public DateTime ComparisonDate { get; set; }
    
    // Performance vs Benchmark
    public Dictionary<string, BenchmarkMetricDto> Metrics { get; set; } = new();
    
    // Overall Assessment
    public string OverallRating { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public int IndustryPercentileRank { get; set; }
    
    // Insights
    public List<string> StrengthAreas { get; set; } = new();
    public List<string> ImprovementAreas { get; set; } = new();
    public List<string> ActionableInsights { get; set; } = new();
    
    // ROI Projections
    public decimal EstimatedAnnualSavings { get; set; }
    public int PaybackPeriodMonths { get; set; }
}

/// <summary>
/// Individual benchmark metric
/// </summary>
public class BenchmarkMetricDto
{
    public string MetricName { get; set; } = string.Empty;
    public double ActualValue { get; set; }
    public double BenchmarkValue { get; set; }
    public double PerformanceGap { get; set; }
    public string PerformanceRating { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Error response for API failures
/// </summary>
public class AssetAnalyticsErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }
}

#endregion

#region Supporting DTOs

/// <summary>
/// Asset performance trend data
/// </summary>
public class AssetPerformanceTrendDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<TrendDataPointDto> TrendData { get; set; } = new();
    public string OverallTrend { get; set; } = string.Empty;
    public double TrendStrength { get; set; }
}

/// <summary>
/// Asset performance prediction response
/// </summary>
public class AssetPerformancePredictionDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime PredictionDate { get; set; }
    public int PredictionHorizonDays { get; set; }
    
    // Predicted Values
    public double PredictedOee { get; set; }
    public double PredictedAvailability { get; set; }
    public decimal PredictedMaintenanceCost { get; set; }
    
    // Prediction Quality
    public double ConfidenceLevel { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    
    // Additional property for service compatibility
    public double PredictedPerformance { get; set; }
}

/// <summary>
/// Asset health data point
/// </summary>
public class AssetHealthDataPointDto
{
    public DateTime Date { get; set; }
    public double HealthScore { get; set; }
    public string TrendDirection { get; set; } = string.Empty;
}

/// <summary>
/// Asset KPI result
/// </summary>
public class AssetKpiResultDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public List<AssetPerformanceKpiDto> Kpis { get; set; } = new();
}

/// <summary>
/// Root cause factor
/// </summary>
public class RootCauseFactorDto
{
    public string Factor { get; set; } = string.Empty;
    public double Probability { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Asset optimization recommendation
/// </summary>
public class AssetOptimizationRecommendationDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string RecommendationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal PotentialSavings { get; set; }
    public int ImplementationTimeMonths { get; set; }
    public decimal ImplementationCost { get; set; }
    public decimal ROI { get; set; }
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// Reliability trend analysis DTO
/// </summary>
public class ReliabilityTrendAnalysisDto
{
    public string TrendDirection { get; set; } = string.Empty;
    public double TrendStrength { get; set; }
    public string FailureFrequencyTrend { get; set; } = string.Empty;
    public string RepairTimeTrend { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Benchmark data DTO
/// </summary>
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

/// <summary>
/// Monthly performance metric DTO
/// </summary>
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

/// <summary>
/// Performance trend analysis DTO
/// </summary>
public class PerformanceTrendAnalysisDto
{
    public string TrendDirection { get; set; } = string.Empty;
    public double TrendStrength { get; set; }
    public double MonthlyChange { get; set; }
}

#endregion
