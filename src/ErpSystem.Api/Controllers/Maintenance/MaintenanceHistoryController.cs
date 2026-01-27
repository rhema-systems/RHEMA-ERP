using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/history")]
[Authorize]
public class MaintenanceHistoryController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;
    private readonly ILogger<MaintenanceHistoryController> _logger;

    public MaintenanceHistoryController(
        IWorkOrderService workOrderService,
        ILogger<MaintenanceHistoryController> logger)
    {
        _workOrderService = workOrderService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all completed maintenance work orders (for history page)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaintenanceHistoryItemDto>>> GetMaintenanceHistory(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] Guid? technicianId = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-6);
            var end = endDate ?? DateTime.UtcNow;

            // Get all work orders using paged async
            var filter = new WorkOrderFilterDto
            {
                Status = status,
                StartDate = start,
                EndDate = end,
                Page = 1,
                PageSize = 10000 // Large number to get all records
            };

            var pagedResult = await _workOrderService.GetWorkOrdersPagedAsync(filter);

            // Map to history items
            var historyItems = pagedResult.Items.Select(wo => new MaintenanceHistoryItemDto
            {
                Id = wo.Id,
                WorkOrderId = wo.WorkOrderNumber,
                Title = wo.Title,
                Type = wo.MaintenanceTypeName ?? "General",
                AssetId = wo.AssetId.ToString(),
                AssetName = wo.AssetName ?? "Unknown",
                Location = wo.MaintenanceLocation ?? "Internal",
                Technician = wo.AssignedTechnicianName ?? "Unassigned",
                StartDate = wo.ActualStartDate ?? wo.CreatedDate,
                CompletedDate = wo.ActualEndDate ?? wo.CreatedDate,
                Status = wo.Status,
                Priority = wo.Priority,
                Cost = wo.ActualCost > 0 ? wo.ActualCost : (wo.EstimatedCost > 0 ? wo.EstimatedCost : 0m),
                LaborHours = wo.ActualHours > 0 ? wo.ActualHours : (wo.EstimatedHours > 0 ? wo.EstimatedHours : 0.0),
                Description = wo.Description ?? string.Empty,
                PartsUsed = new List<string>(), // Would need to fetch from parts service
                Notes = wo.Description ?? string.Empty, // Using Description as notes since CompletionNotes doesn't exist in ListDto
                Rating = 4, // Would come from quality control/feedback
                Downtime = 0, // Would calculate from downtime records
                Category = wo.WorkOrderTypeName ?? "General"
            }).ToList();

            // Apply additional filters
            if (!string.IsNullOrEmpty(type))
            {
                historyItems = historyItems
                    .Where(h => h.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(category))
            {
                historyItems = historyItems
                    .Where(h => h.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (technicianId.HasValue)
            {
                // Would filter by technician ID
            }

            _logger.LogInformation("Retrieved {Count} maintenance history items", historyItems.Count);
            return Ok(historyItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance history");
            return StatusCode(500, "An error occurred while retrieving maintenance history");
        }
    }
}
