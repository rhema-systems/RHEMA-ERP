using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Controllers.Maintenance.Mobile;

[ApiController]
[Route("api/mobile/maintenance/work-orders")]
[Authorize]
public class MobileWorkOrderController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MobileWorkOrderController> _logger;

    public MobileWorkOrderController(
        IWorkOrderService workOrderService,
        ICurrentUserService currentUserService,
        ILogger<MobileWorkOrderController> logger)
    {
        _workOrderService = workOrderService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Gets work orders assigned to the current technician (mobile optimized)
    /// </summary>
    [HttpGet("my-work-orders")]
    public async Task<ActionResult<IEnumerable<MobileWorkOrderDto>>> GetMyWorkOrders(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
                return Unauthorized("User not authenticated");

            var workOrders = await _workOrderService.GetWorkOrdersByTechnicianAsync(currentUserId);
            
            if (!string.IsNullOrEmpty(status))
            {
                workOrders = workOrders.Where(wo => wo.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            var mobileWorkOrders = workOrders
                .Take(limit)
                .Select(wo => new MobileWorkOrderDto
                {
                    Id = wo.Id,
                    WorkOrderNumber = wo.WorkOrderNumber,
                    Title = wo.Title,
                    Description = wo.Description,
                    Priority = wo.Priority,
                    Status = wo.Status,
                    AssetName = wo.AssetName,
                    AssetLocation = "", // WorkOrderListDto does not contain AssetLocation
                    ScheduledStartDate = wo.ScheduledStartDate,
                    ScheduledEndDate = wo.ScheduledEndDate,
                    EstimatedHours = wo.EstimatedHours,
                    IsOverdue = wo.ScheduledEndDate < DateTime.Now && wo.Status != "Completed" && wo.Status != "Cancelled",
                    UrgencyLevel = GetUrgencyLevel(wo.Priority, wo.ScheduledEndDate)
                });

            return Ok(mobileWorkOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving mobile work orders for user {UserId}", _currentUserService.UserId);
            return StatusCode(500, "An error occurred while retrieving work orders");
        }
    }

    /// <summary>
    /// Gets detailed work order information for mobile view
    /// </summary>
    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<MobileWorkOrderDetailDto>> GetWorkOrderDetails(Guid id)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(id);
            if (workOrder == null)
                return NotFound($"Work order with ID {id} not found");

            // Check if current user is assigned to this work order
            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
                return Unauthorized("User not authenticated");
                
            if (workOrder.AssignedTechnicianId != currentUserId)
                return Forbid("You are not assigned to this work order");

            var mobileDetail = new MobileWorkOrderDetailDto
            {
                Id = workOrder.Id,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                Title = workOrder.Title,
                Description = workOrder.Description,
                Priority = workOrder.Priority,
                Status = workOrder.Status,
                Type = workOrder.Type,
                AssetId = workOrder.AssetId,
                AssetName = workOrder.AssetName,
                AssetNumber = workOrder.AssetNumber,
                AssetLocation = "", // WorkOrderListDto does not contain AssetLocation
                ScheduledStartDate = workOrder.ScheduledStartDate,
                ScheduledEndDate = workOrder.ScheduledEndDate,
                ActualStartDate = workOrder.ActualStartDate,
                ActualEndDate = workOrder.ActualEndDate,
                EstimatedHours = workOrder.EstimatedHours,
                ActualHours = workOrder.ActualHours,
                EstimatedCost = workOrder.EstimatedCost,
                ActualCost = workOrder.ActualCost,
                Instructions = workOrder.Instructions,
                Notes = workOrder.Notes,
                IsOverdue = workOrder.ScheduledEndDate < DateTime.Now && workOrder.Status != "Completed" && workOrder.Status != "Cancelled",
                CanStart = workOrder.Status == "Scheduled",
                CanComplete = workOrder.Status == "In Progress",
                RequiredSkills = workOrder.RequiredSkills?.Split(',').ToList() ?? new List<string>(),
                SafetyNotes = workOrder.SafetyNotes
            };

            return Ok(mobileDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work order details {WorkOrderId} for mobile", id);
            return StatusCode(500, "An error occurred while retrieving work order details");
        }
    }

    /// <summary>
    /// Starts a work order from mobile device
    /// </summary>
    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<MobileWorkOrderDto>> StartWorkOrder(Guid id, [FromBody] StartWorkOrderMobileDto startDto)
    {
        try
        {
            var workOrder = await _workOrderService.StartWorkOrderAsync(id);
            
            // Log mobile start action
            _logger.LogInformation("Work order {WorkOrderId} started from mobile device by user {UserId} at location {Location}", 
                id, _currentUserService.UserId, startDto.Location);

            var mobileResult = new MobileWorkOrderDto
            {
                Id = workOrder.Id,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                Title = workOrder.Title,
                Status = workOrder.Status,
                ActualStartDate = workOrder.ActualStartDate
            };

            return Ok(mobileResult);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting work order {WorkOrderId} from mobile", id);
            return StatusCode(500, "An error occurred while starting the work order");
        }
    }

    /// <summary>
    /// Completes a work order from mobile device
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<MobileWorkOrderDto>> CompleteWorkOrder(Guid id, [FromBody] CompleteWorkOrderMobileDto completeDto)
    {
        try
        {
            var completeWorkOrderDto = new CompleteWorkOrderDto
            {
                CompletionNotes = completeDto.CompletionNotes,
                ActualHours = completeDto.ActualHours,
                ActualCost = completeDto.ActualCost,
                PartsUsed = completeDto.PartsUsed,
                WorkPerformed = completeDto.WorkPerformed
            };

            var workOrder = await _workOrderService.CompleteWorkOrderAsync(id, completeWorkOrderDto);
            
            _logger.LogInformation("Work order {WorkOrderId} completed from mobile device by user {UserId}", 
                id, _currentUserService.UserId);

            var mobileResult = new MobileWorkOrderDto
            {
                Id = workOrder.Id,
                WorkOrderNumber = workOrder.WorkOrderNumber,
                Title = workOrder.Title,
                Status = workOrder.Status,
                ActualEndDate = workOrder.ActualEndDate,
                ActualHours = workOrder.ActualHours
            };

            return Ok(mobileResult);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing work order {WorkOrderId} from mobile", id);
            return StatusCode(500, "An error occurred while completing the work order");
        }
    }

    /// <summary>
    /// Updates work order progress from mobile device
    /// </summary>
    [HttpPost("{id:guid}/update-progress")]
    public async Task<ActionResult> UpdateProgress(Guid id, [FromBody] UpdateProgressMobileDto progressDto)
    {
        try
        {
            // This would typically update a progress tracking table
            _logger.LogInformation("Progress update for work order {WorkOrderId}: {Progress}% - {Notes}", 
                id, progressDto.ProgressPercentage, progressDto.ProgressNotes);

            // For now, just acknowledge the update
            return Ok(new { message = "Progress updated successfully", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for work order {WorkOrderId} from mobile", id);
            return StatusCode(500, "An error occurred while updating progress");
        }
    }

    /// <summary>
    /// Gets today's schedule for the current technician
    /// </summary>
    [HttpGet("today-schedule")]
    public async Task<ActionResult<IEnumerable<MobileTodayScheduleDto>>> GetTodaySchedule()
    {
        try
        {
            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString) || !Guid.TryParse(currentUserIdString, out var currentUserId))
                return Unauthorized("User not authenticated");
                
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var workOrders = await _workOrderService.GetWorkOrdersByTechnicianAsync(currentUserId);
            var todaySchedule = workOrders
                .Where(wo => wo.ScheduledStartDate >= today && wo.ScheduledStartDate < tomorrow)
                .OrderBy(wo => wo.ScheduledStartDate)
                .Select(wo => new MobileTodayScheduleDto
                {
                    Id = wo.Id,
                    WorkOrderNumber = wo.WorkOrderNumber,
                    Title = wo.Title,
                    AssetName = wo.AssetName,
                    AssetLocation = "", // WorkOrderListDto does not contain AssetLocation
                    ScheduledStartTime = wo.ScheduledStartDate?.ToString("HH:mm"),
                    EstimatedDuration = $"{wo.EstimatedHours:F1}h",
                    Priority = wo.Priority,
                    Status = wo.Status,
                    IsCompleted = wo.Status == "Completed",
                    IsInProgress = wo.Status == "In Progress"
                });

            return Ok(todaySchedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving today's schedule for user {UserId}", _currentUserService.UserId);
            return StatusCode(500, "An error occurred while retrieving today's schedule");
        }
    }

    /// <summary>
    /// Submits offline sync data from mobile device
    /// </summary>
    [HttpPost("offline-sync")]
    public async Task<ActionResult<OfflineSyncResultDto>> SubmitOfflineData([FromBody] OfflineSyncDataDto syncData)
    {
        try
        {
            var results = new OfflineSyncResultDto
            {
                ProcessedItems = 0,
                SuccessfulItems = 0,
                FailedItems = new List<OfflineSyncErrorDto>()
            };

            // Process work order updates
            foreach (var update in syncData.WorkOrderUpdates ?? new List<OfflineWorkOrderUpdateDto>())
            {
                results.ProcessedItems++;
                try
                {
                    // Process the offline update
                    await ProcessOfflineWorkOrderUpdate(update);
                    results.SuccessfulItems++;
                }
                catch (Exception ex)
                {
                    results.FailedItems.Add(new OfflineSyncErrorDto
                    {
                        ItemId = update.WorkOrderId.ToString(),
                        ItemType = "WorkOrderUpdate",
                        Error = ex.Message
                    });
                    _logger.LogError(ex, "Failed to process offline work order update for {WorkOrderId}", update.WorkOrderId);
                }
            }

            _logger.LogInformation("Offline sync completed: {Successful}/{Total} items processed successfully", 
                results.SuccessfulItems, results.ProcessedItems);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing offline sync data");
            return StatusCode(500, "An error occurred while processing offline data");
        }
    }

    private async Task ProcessOfflineWorkOrderUpdate(OfflineWorkOrderUpdateDto update)
    {
        switch (update.UpdateType.ToLower())
        {
            case "start":
                await _workOrderService.StartWorkOrderAsync(update.WorkOrderId);
                break;
            case "complete":
                var completeDto = new CompleteWorkOrderDto
                {
                    CompletionNotes = update.Notes,
                    ActualHours = update.ActualHours,
                    WorkPerformed = update.WorkPerformed
                };
                await _workOrderService.CompleteWorkOrderAsync(update.WorkOrderId, completeDto);
                break;
            case "progress":
                // Handle progress update
                _logger.LogInformation("Progress update processed for work order {WorkOrderId}", update.WorkOrderId);
                break;
            default:
                throw new ArgumentException($"Unknown update type: {update.UpdateType}");
        }
    }

    private static string GetUrgencyLevel(string priority, DateTime? scheduledEndDate)
    {
        if (priority.Equals("Emergency", StringComparison.OrdinalIgnoreCase))
            return "Critical";

        if (scheduledEndDate.HasValue && scheduledEndDate.Value < DateTime.Now)
            return "Overdue";

        if (scheduledEndDate.HasValue && scheduledEndDate.Value < DateTime.Now.AddHours(4))
            return "Urgent";

        return priority.Equals("High", StringComparison.OrdinalIgnoreCase) ? "High" : "Normal";
    }
}

#region Mobile DTOs

public class MobileWorkOrderDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public double? EstimatedHours { get; set; }
    public double? ActualHours { get; set; }
    public bool IsOverdue { get; set; }
    public string UrgencyLevel { get; set; } = string.Empty;
}

public class MobileWorkOrderDetailDto : MobileWorkOrderDto
{
    public string Type { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetNumber { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string Instructions { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool CanStart { get; set; }
    public bool CanComplete { get; set; }
    public List<string> RequiredSkills { get; set; } = new List<string>();
    public string SafetyNotes { get; set; } = string.Empty;
}

public class StartWorkOrderMobileDto
{
    public string? Location { get; set; }
    public string? Notes { get; set; }
}

public class CompleteWorkOrderMobileDto
{
    public string CompletionNotes { get; set; } = string.Empty;
    public double? ActualHours { get; set; }
    public decimal? ActualCost { get; set; }
    public string PartsUsed { get; set; } = string.Empty;
    public string WorkPerformed { get; set; } = string.Empty;
}

public class UpdateProgressMobileDto
{
    public int ProgressPercentage { get; set; }
    public string ProgressNotes { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public class MobileTodayScheduleDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
    public string ScheduledStartTime { get; set; } = string.Empty;
    public string EstimatedDuration { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsInProgress { get; set; }
}

public class OfflineSyncDataDto
{
    public List<OfflineWorkOrderUpdateDto>? WorkOrderUpdates { get; set; }
    public DateTime SyncTimestamp { get; set; }
}

public class OfflineWorkOrderUpdateDto
{
    public Guid WorkOrderId { get; set; }
    public string UpdateType { get; set; } = string.Empty; // "start", "complete", "progress"
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
    public double? ActualHours { get; set; }
    public string? WorkPerformed { get; set; }
    public int? ProgressPercentage { get; set; }
}

public class OfflineSyncResultDto
{
    public int ProcessedItems { get; set; }
    public int SuccessfulItems { get; set; }
    public List<OfflineSyncErrorDto> FailedItems { get; set; } = new List<OfflineSyncErrorDto>();
}

public class OfflineSyncErrorDto
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

#endregion