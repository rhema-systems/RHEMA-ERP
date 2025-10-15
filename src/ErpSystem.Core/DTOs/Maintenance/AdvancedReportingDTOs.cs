using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

#region Predictive Maintenance DTOs

/// <summary>
/// Predictive maintenance insights
/// </summary>
public class PredictiveMaintenanceInsightDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string PredictionType { get; set; } = string.Empty; // Failure, Maintenance, Performance
    public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High, Critical
    public double ConfidenceScore { get; set; }
    public DateTime PredictedDate { get; set; }
    public string? Description { get; set; }
    public string? RecommendedAction { get; set; }
    public decimal EstimatedCostImpact { get; set; }
    public int DaysUntilAction { get; set; }
    public string DataSources { get; set; } = string.Empty; // JSON array
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Asset condition monitoring data
/// </summary>
public class AssetConditionDataDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime ReadingDate { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double? MinThreshold { get; set; }
    public double? MaxThreshold { get; set; }
    public string Status { get; set; } = string.Empty; // Normal, Warning, Critical
    public string? Notes { get; set; }
}

/// <summary>
/// Equipment lifecycle analysis
/// </summary>
public class EquipmentLifecycleAnalysisDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public int AgeInMonths { get; set; }
    public string LifecycleStage { get; set; } = string.Empty; // New, Mature, Aging, EndOfLife
    public double RemainingLifePercentage { get; set; }
    public DateTime? EstimatedReplacementDate { get; set; }
    public decimal AccumulatedMaintenanceCost { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal CurrentValue { get; set; }
    public double DepreciationRate { get; set; }
    public int TotalWorkOrders { get; set; }
    public double AverageDowntimeHours { get; set; }
    public double ReliabilityScore { get; set; }
    public string ReplacementRecommendation { get; set; } = string.Empty;
}

#endregion

#region Advanced Analytics DTOs

/// <summary>
/// Maintenance trend analysis
/// </summary>
public class MaintenanceTrendAnalysisDto
{
    public string AnalysisPeriod { get; set; } = string.Empty; // Daily, Weekly, Monthly, Yearly
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TrendDataPointDto> CostTrend { get; set; } = new();
    public List<TrendDataPointDto> WorkOrderTrend { get; set; } = new();
    public List<TrendDataPointDto> DowntimeTrend { get; set; } = new();
    public List<TrendDataPointDto> EfficiencyTrend { get; set; } = new();
    public string TrendDirection { get; set; } = string.Empty; // Improving, Stable, Declining
    public double TrendStrength { get; set; }
    public string? Insights { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Trend data point
/// </summary>
public class TrendDataPointDto
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public string? Label { get; set; }
    
    // Additional property for service compatibility
    public string MetricType { get; set; } = string.Empty;
}

/// <summary>
/// Root cause analysis
/// </summary>
public class RootCauseAnalysisDto
{
    public string ProblemDescription { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public List<FailureModeDto> FailureModes { get; set; } = new();
    public List<RootCauseDto> RootCauses { get; set; } = new();
    public List<CorrectiveActionDto> RecommendedActions { get; set; } = new();
    public decimal EstimatedCostSavings { get; set; }
    public string AnalysisMethod { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// Failure mode information
/// </summary>
public class FailureModeDto
{
    public string Description { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public string Severity { get; set; } = string.Empty;
    public double Impact { get; set; }
    public string? Causes { get; set; }
}

/// <summary>
/// Root cause information
/// </summary>
public class RootCauseDto
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Likelihood { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public int Priority { get; set; }
}

/// <summary>
/// Corrective action information
/// </summary>
public class CorrectiveActionDto
{
    public string Description { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public int EstimatedDays { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string ResponsibleParty { get; set; } = string.Empty;
}

#endregion

#region Performance Analytics DTOs

/// <summary>
/// Overall Equipment Effectiveness (OEE) analysis
/// </summary>
public class OeeAnalysisDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double Availability { get; set; }
    public double Performance { get; set; }
    public double Quality { get; set; }
    public double OeeScore { get; set; }
    public double PlannedProductionTime { get; set; }
    public double ActualRunTime { get; set; }
    public double IdealCycleTime { get; set; }
    public double ActualCycleTime { get; set; }
    public int GoodUnits { get; set; }
    public int TotalUnits { get; set; }
    public List<OeeLossDto> Losses { get; set; } = new();
    public string PerformanceCategory { get; set; } = string.Empty; // WorldClass, Good, Average, Poor
}

/// <summary>
/// OEE loss information
/// </summary>
public class OeeLossDto
{
    public string LossCategory { get; set; } = string.Empty;
    public string LossType { get; set; } = string.Empty;
    public double Duration { get; set; }
    public double ImpactPercentage { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Energy consumption analysis
/// </summary>
public class EnergyConsumptionAnalysisDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public double TotalEnergyConsumption { get; set; }
    public string EnergyUnit { get; set; } = "kWh";
    public double AverageHourlyConsumption { get; set; }
    public double PeakConsumption { get; set; }
    public decimal TotalEnergyCost { get; set; }
    public double EfficiencyRating { get; set; }
    public List<EnergyConsumptionDataPointDto> ConsumptionData { get; set; } = new();
    public string? EnergyTrend { get; set; }
    public List<string> EnergyOptimizationRecommendations { get; set; } = new();
}

/// <summary>
/// Energy consumption data point
/// </summary>
public class EnergyConsumptionDataPointDto
{
    public DateTime Timestamp { get; set; }
    public double Consumption { get; set; }
    public decimal Cost { get; set; }
    public string? Notes { get; set; }
}

#endregion

#region Executive Dashboards DTOs

/// <summary>
/// Executive maintenance dashboard
/// </summary>
public class ExecutiveMaintenanceDashboardDto
{
    public DateTime ReportDate { get; set; }
    public MaintenanceFinancialSummaryDto FinancialSummary { get; set; } = new();
    public MaintenanceOperationalSummaryDto OperationalSummary { get; set; } = new();
    public List<CriticalIssueDto> CriticalIssues { get; set; } = new();
    public List<KeyPerformanceIndicatorDto> KPIs { get; set; } = new();
    public List<MaintenanceInitiativeDto> StrategicInitiatives { get; set; } = new();
    public string OverallHealthScore { get; set; } = string.Empty;
    public string? ExecutiveSummary { get; set; }
}

/// <summary>
/// Financial summary for maintenance
/// </summary>
public class MaintenanceFinancialSummaryDto
{
    public decimal TotalMaintenanceBudget { get; set; }
    public decimal ActualSpend { get; set; }
    public decimal BudgetVariance { get; set; }
    public double BudgetVariancePercentage { get; set; }
    public decimal PreventiveMaintenanceCost { get; set; }
    public decimal CorrectiveMaintenanceCost { get; set; }
    public decimal ContractorCosts { get; set; }
    public decimal MaterialsCosts { get; set; }
    public decimal LaborCosts { get; set; }
    public decimal CostPerAsset { get; set; }
    public string SpendTrend { get; set; } = string.Empty;
}

/// <summary>
/// Operational summary for maintenance
/// </summary>
public class MaintenanceOperationalSummaryDto
{
    public double OverallEquipmentEffectiveness { get; set; }
    public double PlannedMaintenanceCompliance { get; set; }
    public double FirstTimeFixRate { get; set; }
    public double MeanTimeBetweenFailures { get; set; }
    public double MeanTimeToRepair { get; set; }
    public int TotalWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public double AverageResponseTime { get; set; }
    public int CriticalIssuesCount { get; set; }
    public double AssetAvailability { get; set; }
}

/// <summary>
/// Critical issue information
/// </summary>
public class CriticalIssueDto
{
    public Guid Id { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime IdentifiedDate { get; set; }
    public int DaysOpen { get; set; }
    public decimal PotentialImpact { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
}

/// <summary>
/// Key Performance Indicator
/// </summary>
public class KeyPerformanceIndicatorDto
{
    public string Name { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double TargetValue { get; set; }
    public double PreviousValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Trend { get; set; } = string.Empty; // Up, Down, Stable
    public string Status { get; set; } = string.Empty; // OnTarget, AboveTarget, BelowTarget
    public string? Commentary { get; set; }
}

/// <summary>
/// Strategic maintenance initiative
/// </summary>
public class MaintenanceInitiativeDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public decimal Budget { get; set; }
    public decimal ActualCost { get; set; }
    public string? ExpectedBenefits { get; set; }
}

#endregion

#region Benchmarking DTOs

/// <summary>
/// Industry benchmarking data
/// </summary>
public class IndustryBenchmarkDto
{
    public string Industry { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double IndustryAverage { get; set; }
    public double IndustryBest { get; set; }
    public double OrganizationValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double PercentileRanking { get; set; }
    public string PerformanceCategory { get; set; } = string.Empty; // TopQuartile, AboveAverage, Average, BelowAverage
    public string? ImprovementOpportunity { get; set; }
    public DateTime LastUpdated { get; set; }
}

#endregion

#region Compliance and Regulatory DTOs

/// <summary>
/// Regulatory compliance report
/// </summary>
public class RegulatoryComplianceReportDto
{
    public string RegulatoryFramework { get; set; } = string.Empty;
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public double OverallComplianceScore { get; set; }
    public List<ComplianceRequirementDto> Requirements { get; set; } = new();
    public List<RegulatoryViolationDto> Violations { get; set; } = new();
    public List<ComplianceActionDto> CorrectiveActions { get; set; } = new();
    public string ComplianceStatus { get; set; } = string.Empty;
    public DateTime? NextAuditDate { get; set; }
}

/// <summary>
/// Compliance requirement information
/// </summary>
public class ComplianceRequirementDto
{
    public string RequirementId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Compliant, NonCompliant, PartiallyCompliant
    public DateTime? LastVerificationDate { get; set; }
    public DateTime? NextVerificationDue { get; set; }
    public string? Evidence { get; set; }
}

/// <summary>
/// Regulatory compliance violation information
/// </summary>
public class RegulatoryViolationDto
{
    public string ViolationId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime IdentifiedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? TargetResolutionDate { get; set; }
    public string? CorrectiveAction { get; set; }
}

/// <summary>
/// Compliance action information
/// </summary>
public class ComplianceActionDto
{
    public string ActionId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string ResponsibleParty { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
}

#endregion

#region Missing Report DTOs

/// <summary>
/// Technician performance report DTO
/// </summary>
public class TechnicianPerformanceReportDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime ReportPeriodStart { get; set; }
    public DateTime ReportPeriodEnd { get; set; }
    public int CompletedWorkOrders { get; set; }
    public double AverageCompletionTime { get; set; }
    public double FirstTimeFixRate { get; set; }
    public double CustomerSatisfactionScore { get; set; }
    public List<string> Skills { get; set; } = new();
    public string PerformanceRating { get; set; } = string.Empty;
}

/// <summary>
/// Safety report DTO
/// </summary>
public class SafetyReportDto
{
    public DateTime ReportDate { get; set; }
    public int TotalIncidents { get; set; }
    public int NearMisses { get; set; }
    public int SafetyViolations { get; set; }
    public string OverallSafetyScore { get; set; } = string.Empty;
    public List<string> SafetyRecommendations { get; set; } = new();
}

/// <summary>
/// Work order analysis DTO
/// </summary>
public class WorkOrderAnalysisDto
{
    public DateTime AnalysisPeriodStart { get; set; }
    public DateTime AnalysisPeriodEnd { get; set; }
    public int TotalWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public double AverageCompletionTime { get; set; }
    public decimal TotalCost { get; set; }
    public string AnalysisSummary { get; set; } = string.Empty;
}

/// <summary>
/// Report execution result DTO
/// </summary>
public class ReportExecutionResultDto
{
    public Guid ReportId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Maintenance analytics DTO
/// </summary>
public class MaintenanceAnalyticsDto
{
    public DateTime AnalysisDate { get; set; }
    public List<KeyPerformanceIndicatorDto> KPIs { get; set; } = new();
    public List<TrendDataPointDto> Trends { get; set; } = new();
    public string AnalyticsSummary { get; set; } = string.Empty;
}

/// <summary>
/// Custom report request DTO
/// </summary>
public class CustomReportRequestDto
{
    public string ReportName { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> Parameters { get; set; } = new();
    public string OutputFormat { get; set; } = string.Empty;
}

/// <summary>
/// Scheduled report DTO
/// </summary>
public class ScheduledReportDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
}

/// <summary>
/// Create scheduled report DTO
/// </summary>
public class CreateScheduledReportDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string Schedule { get; set; } = string.Empty;
    
    public string ReportType { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
}

/// <summary>
/// Create maintenance report template DTO
/// </summary>
public class CreateMaintenanceReportTemplateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string ReportType { get; set; } = string.Empty;
    
    public string Template { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
}

/// <summary>
/// Execute maintenance report DTO
/// </summary>
public class ExecuteMaintenanceReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> Parameters { get; set; } = new();
    public string OutputFormat { get; set; } = "PDF";
}

#endregion
