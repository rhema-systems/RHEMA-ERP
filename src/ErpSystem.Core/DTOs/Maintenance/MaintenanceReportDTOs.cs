using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

#region Analytics and Reporting DTOs

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
    public IEnumerable<MaintenanceHistoryDto> MaintenanceHistory { get; set; } = new List<MaintenanceHistoryDto>();
}

public class MaintenanceCostAnalysisDto
{
    public decimal TotalCost { get; set; }
    public decimal PreventiveCost { get; set; }
    public decimal CorrectiveCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal MaterialsCost { get; set; }
    public decimal ContractorCost { get; set; }
    public IEnumerable<CostByAssetDto> CostByAsset { get; set; } = new List<CostByAssetDto>();
    public IEnumerable<CostByMonthDto> CostByMonth { get; set; } = new List<CostByMonthDto>();
    public IEnumerable<CostByCategoryDto> CostByCategory { get; set; } = new List<CostByCategoryDto>();
}

public class PreventiveMaintenanceReportDto
{
    public int TotalScheduledMaintenance { get; set; }
    public int CompletedScheduledMaintenance { get; set; }
    public int OverdueScheduledMaintenance { get; set; }
    public double ScheduleCompliance { get; set; }
    public decimal PreventiveCostSavings { get; set; }
    public IEnumerable<AssetScheduleComplianceDto> AssetCompliance { get; set; } = new List<AssetScheduleComplianceDto>();
    public IEnumerable<UpcomingMaintenanceDto> UpcomingMaintenance { get; set; } = new List<UpcomingMaintenanceDto>();
}

public class WorkOrderTrendsDto
{
    public IEnumerable<WorkOrderTrendDataDto> MonthlyTrends { get; set; } = new List<WorkOrderTrendDataDto>();
    public IEnumerable<WorkOrderByTypeDto> WorkOrdersByType { get; set; } = new List<WorkOrderByTypeDto>();
    public IEnumerable<WorkOrderByPriorityDto> WorkOrdersByPriority { get; set; } = new List<WorkOrderByPriorityDto>();
    public IEnumerable<WorkOrderByStatusDto> WorkOrdersByStatus { get; set; } = new List<WorkOrderByStatusDto>();
    public double AverageCompletionTime { get; set; }
    public double FirstTimeFixRate { get; set; }
}

public class TechnicianUtilizationReportDto
{
    public double OverallUtilization { get; set; }
    public IEnumerable<TechnicianUtilizationDto> TechnicianUtilization { get; set; } = new List<TechnicianUtilizationDto>();
    public IEnumerable<SkillUtilizationDto> SkillUtilization { get; set; } = new List<SkillUtilizationDto>();
    public IEnumerable<TeamUtilizationDto> TeamUtilization { get; set; } = new List<TeamUtilizationDto>();
}

public class AssetReliabilityReportDto
{
    public double OverallReliability { get; set; }
    public IEnumerable<AssetReliabilityDto> AssetReliability { get; set; } = new List<AssetReliabilityDto>();
    public IEnumerable<AssetCategoryReliabilityDto> CategoryReliability { get; set; } = new List<AssetCategoryReliabilityDto>();
    public IEnumerable<FailureModeAnalysisDto> FailureModes { get; set; } = new List<FailureModeAnalysisDto>();
}

#endregion

#region Supporting DTOs

public class WorkOrderStatusSummaryDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class AssetCategoryMetricsDto
{
    public string Category { get; set; } = string.Empty;
    public int AssetCount { get; set; }
    public int ActiveWorkOrders { get; set; }
    public decimal MaintenanceCost { get; set; }
    public double UptimePercentage { get; set; }
}

public class MaintenanceHistoryDto
{
    public DateTime Date { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CostByAssetDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal Percentage { get; set; }
}

public class CostByMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
}

public class CostByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal Percentage { get; set; }
}

public class AssetScheduleComplianceDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int ScheduledCount { get; set; }
    public int CompletedCount { get; set; }
    public double CompliancePercentage { get; set; }
}

public class UpcomingMaintenanceDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
}

public class WorkOrderTrendDataDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
}

public class WorkOrderByTypeDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class WorkOrderByPriorityDto
{
    public string Priority { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class WorkOrderByStatusDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class TechnicianUtilizationDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public double UtilizationPercentage { get; set; }
    public int CompletedWorkOrders { get; set; }
    public double HoursWorked { get; set; }
    public double AvailableHours { get; set; }
}

public class SkillUtilizationDto
{
    public string Skill { get; set; } = string.Empty;
    public int TechniciansWithSkill { get; set; }
    public int WorkOrdersRequiringSkill { get; set; }
    public double UtilizationPercentage { get; set; }
}

public class TeamUtilizationDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public double UtilizationPercentage { get; set; }
    public int MemberCount { get; set; }
    public int CompletedWorkOrders { get; set; }
}

public class AssetReliabilityDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public double ReliabilityPercentage { get; set; }
    public double MeanTimeBetweenFailures { get; set; }
    public int FailureCount { get; set; }
    public double OperatingHours { get; set; }
}

public class AssetCategoryReliabilityDto
{
    public string Category { get; set; } = string.Empty;
    public double ReliabilityPercentage { get; set; }
    public int AssetCount { get; set; }
    public double AverageMTBF { get; set; }
}

public class FailureModeAnalysisDto
{
    public string FailureMode { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public decimal AverageCost { get; set; }
    public double AverageDowntime { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

#endregion
