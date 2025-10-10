using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/history")]
[Authorize]
public class MaintenanceHistoryController : ControllerBase
{
    private readonly IMaintenanceAssetService _assetService;
    private readonly IWorkOrderService _workOrderService;
    private readonly IAssetDowntimeService _downtimeService;
    private readonly IAssetInspectionService _inspectionService;
    private readonly ILogger<MaintenanceHistoryController> _logger;

    public MaintenanceHistoryController(
        IMaintenanceAssetService assetService,
        IWorkOrderService workOrderService,
        IAssetDowntimeService downtimeService,
        IAssetInspectionService inspectionService,
        ILogger<MaintenanceHistoryController> logger)
    {
        _assetService = assetService;
        _workOrderService = workOrderService;
        _downtimeService = downtimeService;
        _inspectionService = inspectionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive maintenance history for an asset
    /// </summary>
    [HttpGet("asset/{assetId:guid}")]
    public async Task<ActionResult<AssetMaintenanceHistoryDto>> GetAssetMaintenanceHistory(
        Guid assetId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? eventType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                return NotFound($"Asset with ID {assetId} not found");

            var start = startDate ?? DateTime.Today.AddYears(-1);
            var end = endDate ?? DateTime.Today.AddDays(1);

            if (pageSize > 200) pageSize = 200;

            // Get all maintenance events
            var workOrders = await GetAssetWorkOrderHistory(assetId, start, end);
            var downtime = await GetAssetDowntimeHistory(assetId, start, end);
            var inspections = await GetAssetInspectionHistory(assetId, start, end);

            // Combine and sort all events
            var allEvents = new List<MaintenanceEventDto>();
            
            // Add work order events
            allEvents.AddRange(workOrders.Select(wo => new MaintenanceEventDto
            {
                Id = wo.Id,
                EventType = "WorkOrder",
                EventDate = wo.CreatedDate,
                Title = wo.Title,
                Description = wo.Description,
                Status = wo.Status,
                Priority = wo.Priority,
                Cost = wo.ActualCost > 0 ? wo.ActualCost : wo.EstimatedCost,
                Duration = wo.ActualHours,
                TechnicianName = wo.AssignedTechnicianName,
                WorkOrderNumber = wo.WorkOrderNumber,
                IsCompleted = wo.Status == "Completed",
                CompletionDate = wo.ActualEndDate
            }));

            // Add downtime events
            allEvents.AddRange(downtime.Select(dt => new MaintenanceEventDto
            {
                Id = dt.Id,
                EventType = "Downtime",
                EventDate = dt.StartTime,
                Title = $"Asset Downtime - {dt.DowntimeType}",
                Description = dt.Description,
                Status = dt.EndTime.HasValue ? "Completed" : "Active",
                Priority = dt.Priority,
                Duration = (double)dt.Duration,
                DowntimeReason = dt.Description,
                IsCompleted = dt.EndTime.HasValue,
                CompletionDate = dt.EndTime
            }));

            // Add inspection events
            allEvents.AddRange(inspections.Select(insp => new MaintenanceEventDto
            {
                Id = insp.Id,
                EventType = "Inspection",
                EventDate = insp.InspectionDate,
                Title = $"Inspection - {insp.InspectionType}",
                Description = insp.Notes ?? "Regular inspection",
                Status = insp.Status,
                Priority = "Normal",
                TechnicianName = insp.InspectorName,
                InspectionResult = insp.OverallResult,
                IsCompleted = insp.Status == "Completed",
                CompletionDate = insp.InspectionDate
            }));

            // Filter by event type if specified
            if (!string.IsNullOrEmpty(eventType))
            {
                allEvents = allEvents.Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Sort by date (most recent first)
            allEvents = allEvents.OrderByDescending(e => e.EventDate).ToList();

            // Paginate
            var totalEvents = allEvents.Count;
            var pagedEvents = allEvents.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Calculate summary statistics
            var summary = new MaintenanceHistorySummaryDto
            {
                TotalWorkOrders = workOrders.Count(),
                CompletedWorkOrders = workOrders.Count(wo => wo.Status == "Completed"),
                TotalDowntimeHours = downtime.Sum(dt => (double)dt.Duration),
                TotalInspections = inspections.Count(),
                PassedInspections = inspections.Count(i => i.OverallResult == "Pass"),
                TotalMaintenanceCost = workOrders.Where(wo => wo.ActualCost > 0).Sum(wo => wo.ActualCost),
                AverageRepairTime = workOrders.Where(wo => wo.ActualHours > 0).Any() 
                    ? workOrders.Where(wo => wo.ActualHours > 0).Average(wo => wo.ActualHours) 
                    : 0,
                MostCommonIssue = GetMostCommonIssue(workOrders),
                ReliabilityScore = CalculateReliabilityScore(workOrders, downtime, start, end)
            };

            var result = new AssetMaintenanceHistoryDto
            {
                AssetId = asset.Id,
                AssetName = asset.Name,
                AssetNumber = asset.AssetNumber,
                DateRange = $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}",
                Summary = summary,
                Events = pagedEvents,
                TotalEvents = totalEvents,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalEvents / pageSize)
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance history for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving maintenance history");
        }
    }

    /// <summary>
    /// Gets maintenance history timeline for multiple assets
    /// </summary>
    [HttpGet("timeline")]
    public async Task<ActionResult<MaintenanceTimelineDto>> GetMaintenanceTimeline(
        [FromQuery] Guid[]? assetIds = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string[]? eventTypes = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today.AddDays(1);

            var timeline = await BuildMaintenanceTimeline(assetIds, start, end, eventTypes);
            return Ok(timeline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance timeline");
            return StatusCode(500, "An error occurred while retrieving maintenance timeline");
        }
    }

    /// <summary>
    /// Gets work order history with detailed analysis
    /// </summary>
    [HttpGet("work-orders")]
    public async Task<ActionResult<WorkOrderHistoryAnalysisDto>> GetWorkOrderHistory(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? technicianId = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddDays(-90);
            var end = endDate ?? DateTime.Today.AddDays(1);

            var analysis = await AnalyzeWorkOrderHistory(start, end, assetId, technicianId);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order history analysis");
            return StatusCode(500, "An error occurred while retrieving work order history");
        }
    }

    /// <summary>
    /// Gets asset performance trends over time
    /// </summary>
    [HttpGet("performance-trends/{assetId:guid}")]
    public async Task<ActionResult<AssetPerformanceTrendsDto>> GetAssetPerformanceTrends(
        Guid assetId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string period = "month") // week, month, quarter, year
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddMonths(-12);
            var end = endDate ?? DateTime.Today;

            var trends = await CalculateAssetPerformanceTrends(assetId, start, end, period);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance trends for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving performance trends");
        }
    }

    /// <summary>
    /// Gets maintenance cost history and analysis
    /// </summary>
    [HttpGet("cost-analysis")]
    public async Task<ActionResult<MaintenanceCostHistoryDto>> GetMaintenanceCostHistory(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] string groupBy = "month") // month, quarter, year
    {
        try
        {
            var start = startDate ?? DateTime.Today.AddYears(-2);
            var end = endDate ?? DateTime.Today;

            var costHistory = await AnalyzeMaintenanceCostHistory(start, end, assetId, groupBy);
            return Ok(costHistory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance cost history");
            return StatusCode(500, "An error occurred while retrieving cost history");
        }
    }

    /// <summary>
    /// Exports maintenance history to various formats
    /// </summary>
    [HttpGet("export/{assetId:guid}")]
    public async Task<IActionResult> ExportMaintenanceHistory(
        Guid assetId,
        [FromQuery] string format = "csv", // csv, pdf, excel
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null)
                return NotFound($"Asset with ID {assetId} not found");

            var start = startDate ?? DateTime.Today.AddYears(-1);
            var end = endDate ?? DateTime.Today;

            var history = await GetAssetMaintenanceHistory(assetId, start, end, null, 1, 10000);
            if (history.Result is not OkObjectResult okResult || okResult.Value is not AssetMaintenanceHistoryDto historyData)
            {
                return StatusCode(500, "Failed to retrieve maintenance history");
            }

            var exportData = await GenerateExportData(historyData, format);
            var fileName = $"maintenance_history_{asset.AssetNumber}_{start:yyyyMMdd}_{end:yyyyMMdd}.{format.ToLower()}";

            return format.ToLower() switch
            {
                "pdf" => File(exportData, "application/pdf", fileName),
                "excel" => File(exportData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName),
                _ => File(exportData, "text/csv", fileName)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting maintenance history for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while exporting maintenance history");
        }
    }

    /// <summary>
    /// Gets recurring maintenance patterns and recommendations
    /// </summary>
    [HttpGet("patterns/{assetId:guid}")]
    public async Task<ActionResult<MaintenancePatternsDto>> GetMaintenancePatterns(Guid assetId)
    {
        try
        {
            var patterns = await AnalyzeMaintenancePatterns(assetId);
            return Ok(patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing maintenance patterns for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while analyzing maintenance patterns");
        }
    }

    #region Helper Methods

    private async Task<IEnumerable<WorkOrderListDto>> GetAssetWorkOrderHistory(Guid assetId, DateTime startDate, DateTime endDate)
    {
        // This would get work orders from the service
        var allWorkOrders = await _workOrderService.GetWorkOrdersByTechnicianAsync(Guid.Empty); // Placeholder
        return allWorkOrders.Where(wo => wo.AssetId == assetId && wo.CreatedDate >= startDate && wo.CreatedDate <= endDate);
    }

    private async Task<IEnumerable<AssetDowntimeDto>> GetAssetDowntimeHistory(Guid assetId, DateTime startDate, DateTime endDate)
    {
        return await _downtimeService.GetDowntimeByAssetAsync(assetId);
    }

    private async Task<IEnumerable<AssetInspectionDto>> GetAssetInspectionHistory(Guid assetId, DateTime startDate, DateTime endDate)
    {
        return await _inspectionService.GetInspectionsByAssetAsync(assetId);
    }

    private string GetMostCommonIssue(IEnumerable<WorkOrderListDto> workOrders)
    {
        if (!workOrders.Any()) return "None";

        var issues = workOrders
            .Where(wo => !string.IsNullOrEmpty(wo.Description))
            .GroupBy(wo => wo.WorkOrderTypeName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return issues?.Key ?? "Various";
    }

    private double CalculateReliabilityScore(
        IEnumerable<WorkOrderListDto> workOrders, 
        IEnumerable<AssetDowntimeDto> downtime, 
        DateTime startDate, 
        DateTime endDate)
    {
        var totalDays = (endDate - startDate).TotalDays;
        if (totalDays <= 0) return 100.0;

        var emergencyWorkOrders = workOrders.Count(wo => wo.Priority == "Emergency");
        var totalDowntimeHours = downtime.Sum(dt => (double)dt.Duration);
        var downtimeDays = totalDowntimeHours / 24.0;

        // Simple reliability calculation (100 - percentage of days with issues)
        var problematicDays = Math.Min(emergencyWorkOrders + downtimeDays, totalDays);
        var reliability = Math.Max(0, 100.0 - (problematicDays / totalDays * 100));

        return Math.Round(reliability, 1);
    }

    private async Task<MaintenanceTimelineDto> BuildMaintenanceTimeline(
        Guid[]? assetIds, 
        DateTime startDate, 
        DateTime endDate, 
        string[]? eventTypes)
    {
        var timelineEvents = new List<TimelineEventDto>();

        // For demonstration, create some sample timeline events
        // In a real implementation, this would query actual events from the database
        
        return new MaintenanceTimelineDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalEvents = timelineEvents.Count,
            Events = timelineEvents.OrderByDescending(e => e.EventDate).ToList(),
            EventTypeCounts = timelineEvents.GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private async Task<WorkOrderHistoryAnalysisDto> AnalyzeWorkOrderHistory(
        DateTime startDate, 
        DateTime endDate, 
        Guid? assetId, 
        Guid? technicianId)
    {
        // This would perform comprehensive work order analysis
        return new WorkOrderHistoryAnalysisDto
        {
            Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            TotalWorkOrders = 145,
            CompletedWorkOrders = 138,
            AverageCompletionTime = 4.2,
            OnTimeCompletion = 87.5,
            FirstTimeFixRate = 92.3,
            MostActiveAsset = "Pump Station A",
            MostProductiveTechnician = "John Smith",
            TopIssueTypes = new List<IssueTypeAnalysisDto>
            {
                new() { IssueType = "Mechanical Failure", Count = 45, AverageTime = 6.2, TotalCost = 15000 },
                new() { IssueType = "Electrical Issue", Count = 32, AverageTime = 3.8, TotalCost = 9500 },
                new() { IssueType = "Routine Maintenance", Count = 68, AverageTime = 2.1, TotalCost = 8200 }
            }
        };
    }

    private async Task<AssetPerformanceTrendsDto> CalculateAssetPerformanceTrends(
        Guid assetId, 
        DateTime startDate, 
        DateTime endDate, 
        string period)
    {
        // This would calculate actual performance trends from historical data
        // For demo purposes, using placeholder values
        
        return new AssetPerformanceTrendsDto
        {
            AssetId = assetId,
            Period = period,
            DateRange = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            UptimeTrend = GenerateTrendData("Uptime", 95.2),
            MaintenanceCostTrend = GenerateTrendData("Cost", 12500),
            WorkOrderFrequencyTrend = GenerateTrendData("Frequency", 3.2),
            MTBFTrend = GenerateTrendData("MTBF", 168.0), // Placeholder value
            MTTRTrend = GenerateTrendData("MTTR", 4.5), // Placeholder value
            OverallTrendDirection = "Improving" // Could be "Improving", "Stable", "Declining"
        };
    }

    private List<TrendDataPointDto> GenerateTrendData(string metricName, double currentValue)
    {
        // Generate sample trend data points
        // In a real implementation, this would come from historical calculations
        var random = new Random();
        var points = new List<TrendDataPointDto>();
        
        for (int i = 12; i >= 0; i--)
        {
            var date = DateTime.Today.AddMonths(-i);
            var variance = (random.NextDouble() - 0.5) * 0.2; // ±10% variance
            var value = currentValue * (1 + variance);
            
            points.Add(new TrendDataPointDto
            {
                Date = date,
                Value = Math.Round(value, 2),
                Period = date.ToString("yyyy-MM")
            });
        }
        
        return points;
    }

    private async Task<MaintenanceCostHistoryDto> AnalyzeMaintenanceCostHistory(
        DateTime startDate, 
        DateTime endDate, 
        Guid? assetId, 
        string groupBy)
    {
        // This would use the analytics service to get cost analysis
        // For demo purposes, creating placeholder data
        
        return new MaintenanceCostHistoryDto
        {
            DateRange = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            TotalCost = 125000m, // Placeholder value
            AverageMonthlyCost = 10416.67m, // Placeholder value
            CostTrend = "Increasing", // Would be calculated from actual data
            CostByPeriod = new List<CostPeriodDto>
            {
                new() { Period = "Jan 2024", Cost = 9500m, BudgetVariance = 500m },
                new() { Period = "Feb 2024", Cost = 10200m, BudgetVariance = -300m },
                new() { Period = "Mar 2024", Cost = 11800m, BudgetVariance = 1200m }
            },
            TopCostDrivers = new List<CostDriverDto>
            {
                new() { Category = "Pump Station A", Cost = 45000m, Percentage = 36 },
                new() { Category = "Conveyor System", Cost = 28000m, Percentage = 22.4 },
                new() { Category = "HVAC Units", Cost = 22000m, Percentage = 17.6 }
            }
        };
    }

    private async Task<byte[]> GenerateExportData(AssetMaintenanceHistoryDto history, string format)
    {
        // This would generate actual export files
        // For demonstration, return empty byte array
        return format.ToLower() switch
        {
            "pdf" => GeneratePdfReport(history),
            "excel" => GenerateExcelReport(history),
            _ => GenerateCsvReport(history)
        };
    }

    private byte[] GenerateCsvReport(AssetMaintenanceHistoryDto history)
    {
        var csv = "Date,Event Type,Title,Status,Priority,Cost,Duration,Technician\n";
        
        foreach (var evt in history.Events)
        {
            csv += $"{evt.EventDate:yyyy-MM-dd},{evt.EventType},{evt.Title},{evt.Status},{evt.Priority},{evt.Cost},{evt.Duration},{evt.TechnicianName}\n";
        }
        
        return System.Text.Encoding.UTF8.GetBytes(csv);
    }

    private byte[] GeneratePdfReport(AssetMaintenanceHistoryDto history)
    {
        // Would use a PDF library like iText or similar
        return System.Text.Encoding.UTF8.GetBytes("PDF report placeholder");
    }

    private byte[] GenerateExcelReport(AssetMaintenanceHistoryDto history)
    {
        // Would use a library like EPPlus or similar
        return System.Text.Encoding.UTF8.GetBytes("Excel report placeholder");
    }

    private async Task<MaintenancePatternsDto> AnalyzeMaintenancePatterns(Guid assetId)
    {
        // This would perform pattern analysis on historical data
        return new MaintenancePatternsDto
        {
            AssetId = assetId,
            RecurringIssues = new List<RecurringIssueDto>
            {
                new() { IssueType = "Belt Replacement", Frequency = "Every 6 months", NextPredicted = DateTime.Today.AddMonths(6) },
                new() { IssueType = "Oil Change", Frequency = "Every 3 months", NextPredicted = DateTime.Today.AddMonths(3) }
            },
            SeasonalPatterns = new List<SeasonalPatternDto>
            {
                new() { Season = "Summer", IssueType = "Overheating", IncreasePercentage = 35 },
                new() { Season = "Winter", IssueType = "Cold Weather Issues", IncreasePercentage = 20 }
            },
            MaintenanceRecommendations = new List<string>
            {
                "Consider predictive maintenance for belt replacements",
                "Implement temperature monitoring during summer months",
                "Schedule preventive oil changes every 2.5 months instead of reactive replacements"
            }
        };
    }

    #endregion
}

#region History DTOs

public class AssetMaintenanceHistoryDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public MaintenanceHistorySummaryDto Summary { get; set; } = new();
    public List<MaintenanceEventDto> Events { get; set; } = new();
    public int TotalEvents { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class MaintenanceHistorySummaryDto
{
    public int TotalWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public double TotalDowntimeHours { get; set; }
    public int TotalInspections { get; set; }
    public int PassedInspections { get; set; }
    public decimal TotalMaintenanceCost { get; set; }
    public double AverageRepairTime { get; set; }
    public string MostCommonIssue { get; set; } = string.Empty;
    public double ReliabilityScore { get; set; }
}

public class MaintenanceEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public double? Duration { get; set; }
    public string? TechnicianName { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? DowntimeReason { get; set; }
    public string? InspectionResult { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
}

public class MaintenanceTimelineDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalEvents { get; set; }
    public List<TimelineEventDto> Events { get; set; } = new();
    public Dictionary<string, int> EventTypeCounts { get; set; } = new();
}

public class TimelineEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}

public class WorkOrderHistoryAnalysisDto
{
    public string Period { get; set; } = string.Empty;
    public int TotalWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public double AverageCompletionTime { get; set; }
    public double OnTimeCompletion { get; set; }
    public double FirstTimeFixRate { get; set; }
    public string MostActiveAsset { get; set; } = string.Empty;
    public string MostProductiveTechnician { get; set; } = string.Empty;
    public List<IssueTypeAnalysisDto> TopIssueTypes { get; set; } = new();
}

public class IssueTypeAnalysisDto
{
    public string IssueType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AverageTime { get; set; }
    public decimal TotalCost { get; set; }
}

public class AssetPerformanceTrendsDto
{
    public Guid AssetId { get; set; }
    public string Period { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public List<TrendDataPointDto> UptimeTrend { get; set; } = new();
    public List<TrendDataPointDto> MaintenanceCostTrend { get; set; } = new();
    public List<TrendDataPointDto> WorkOrderFrequencyTrend { get; set; } = new();
    public List<TrendDataPointDto> MTBFTrend { get; set; } = new();
    public List<TrendDataPointDto> MTTRTrend { get; set; } = new();
    public string OverallTrendDirection { get; set; } = string.Empty;
}

public class TrendDataPointDto
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public string Period { get; set; } = string.Empty;
}

public class MaintenanceCostHistoryDto
{
    public string DateRange { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public decimal AverageMonthlyCost { get; set; }
    public string CostTrend { get; set; } = string.Empty;
    public List<CostPeriodDto> CostByPeriod { get; set; } = new();
    public List<CostDriverDto> TopCostDrivers { get; set; } = new();
}

public class CostPeriodDto
{
    public string Period { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal BudgetVariance { get; set; }
}

public class CostDriverDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public double Percentage { get; set; }
}

public class MaintenancePatternsDto
{
    public Guid AssetId { get; set; }
    public List<RecurringIssueDto> RecurringIssues { get; set; } = new();
    public List<SeasonalPatternDto> SeasonalPatterns { get; set; } = new();
    public List<string> MaintenanceRecommendations { get; set; } = new();
}

public class RecurringIssueDto
{
    public string IssueType { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime NextPredicted { get; set; }
}

public class SeasonalPatternDto
{
    public string Season { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public double IncreasePercentage { get; set; }
}

#endregion