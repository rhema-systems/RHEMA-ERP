using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/schedules")]
[Authorize]
public class MaintenanceSchedulesController : ControllerBase
{
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly ILogger<MaintenanceSchedulesController> _logger;

    public MaintenanceSchedulesController(
        IMaintenanceScheduleService scheduleService,
        ILogger<MaintenanceSchedulesController> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of maintenance schedules with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MaintenanceScheduleListDto>>> GetSchedules(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? maintenanceTypeId = null,
        [FromQuery] string? frequency = null,
        [FromQuery] string? priority = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isOverdue = null,
        [FromQuery] Guid? assignedTechnicianId = null,
        [FromQuery] Guid? assignedTeamId = null,
        [FromQuery] DateTime? dueDateFrom = null,
        [FromQuery] DateTime? dueDateTo = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var filter = new MaintenanceScheduleFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                AssetId = assetId,
                MaintenanceTypeId = maintenanceTypeId,
                Frequency = frequency,
                Priority = priority,
                IsActive = isActive,
                IsOverdue = isOverdue,
                AssignedTechnicianId = assignedTechnicianId,
                AssignedTeamId = assignedTeamId,
                DueDateFrom = dueDateFrom,
                DueDateTo = dueDateTo
            };

            var result = await _scheduleService.GetSchedulesPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance schedules");
            return StatusCode(500, "An error occurred while retrieving maintenance schedules");
        }
    }

    /// <summary>
    /// Gets all active maintenance schedules
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetActiveSchedules()
    {
        try
        {
            var result = await _scheduleService.GetActiveSchedulesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active maintenance schedules");
            return StatusCode(500, "An error occurred while retrieving active maintenance schedules");
        }
    }

    /// <summary>
    /// Gets schedules due within specified days
    /// </summary>
    [HttpGet("due-in-days/{days}")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetSchedulesDueInDays(int days)
    {
        try
        {
            var result = await _scheduleService.GetSchedulesDueInDaysAsync(days);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules due in {Days} days", days);
            return StatusCode(500, $"An error occurred while retrieving schedules due in {days} days");
        }
    }

    /// <summary>
    /// Gets overdue maintenance schedules
    /// </summary>
    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetOverdueSchedules()
    {
        try
        {
            var result = await _scheduleService.GetOverdueSchedulesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue maintenance schedules");
            return StatusCode(500, "An error occurred while retrieving overdue maintenance schedules");
        }
    }

    /// <summary>
    /// Gets a specific maintenance schedule by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceScheduleDto>> GetSchedule(Guid id)
    {
        try
        {
            var schedule = await _scheduleService.GetScheduleByIdAsync(id);
            if (schedule == null)
                return NotFound($"Maintenance schedule with ID {id} not found");

            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance schedule {ScheduleId}", id);
            return StatusCode(500, $"An error occurred while retrieving maintenance schedule {id}");
        }
    }

    /// <summary>
    /// Creates a new maintenance schedule
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceScheduleDto>> CreateSchedule([FromBody] CreateMaintenanceScheduleDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var schedule = await _scheduleService.CreateScheduleAsync(createDto);
            return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, schedule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance schedule");
            return StatusCode(500, "An error occurred while creating the maintenance schedule");
        }
    }

    /// <summary>
    /// Updates an existing maintenance schedule
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaintenanceScheduleDto>> UpdateSchedule(Guid id, [FromBody] UpdateMaintenanceScheduleDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var schedule = await _scheduleService.UpdateScheduleAsync(id, updateDto);
            return Ok(schedule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while updating the maintenance schedule");
        }
    }

    /// <summary>
    /// Deletes a maintenance schedule
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        try
        {
            await _scheduleService.DeleteScheduleAsync(id);
            return NoContent();
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
            _logger.LogError(ex, "Error deleting maintenance schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while deleting the maintenance schedule");
        }
    }

    /// <summary>
    /// Toggles the active status of a maintenance schedule
    /// </summary>
    [HttpPut("{id:guid}/toggle-status")]
    public async Task<ActionResult<MaintenanceScheduleDto>> ToggleStatus(Guid id)
    {
        try
        {
            var schedule = await _scheduleService.ToggleScheduleStatusAsync(id);
            return Ok(schedule);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling maintenance schedule status {ScheduleId}", id);
            return StatusCode(500, "An error occurred while toggling the maintenance schedule status");
        }
    }

    /// <summary>
    /// Generates work orders from a specific schedule
    /// </summary>
    [HttpPost("{id:guid}/generate-work-orders")]
    public async Task<ActionResult<IEnumerable<WorkOrderDto>>> GenerateWorkOrders(Guid id, [FromQuery] int count = 1)
    {
        try
        {
            var workOrders = await _scheduleService.CreateWorkOrdersFromScheduleAsync(id, count);
            return Ok(workOrders);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating work orders from schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while generating work orders");
        }
    }

    /// <summary>
    /// Gets schedule compliance report
    /// </summary>
    [HttpGet("compliance-report")]
    public async Task<ActionResult<ScheduleComplianceReportDto>> GetComplianceReport(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
            var end = endDate ?? DateTime.UtcNow;

            var report = await _scheduleService.GetScheduleComplianceReportAsync(start, end);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating schedule compliance report");
            
            return StatusCode(500, "An error occurred while generating the schedule compliance report");
        }
    }

    #region Notification and Reminder Endpoints

    /// <summary>
    /// Gets schedules that need reminders sent
    /// </summary>
    [HttpGet("due-for-reminders")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetSchedulesDueForReminders()
    {
        try
        {
            var schedules = await _scheduleService.GetSchedulesDueForRemindersAsync();
            return Ok(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules due for reminders");
            return StatusCode(500, "An error occurred while retrieving schedules due for reminders");
        }
    }

    /// <summary>
    /// Sends a reminder for a specific schedule
    /// </summary>
    [HttpPost("{id:guid}/send-reminder")]
    public async Task<IActionResult> SendScheduleReminder(Guid id, [FromQuery] bool force = false)
    {
        try
        {
            await _scheduleService.SendScheduleReminderAsync(id, force);
            return Ok(new { message = "Reminder sent successfully", scheduleId = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending reminder for schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while sending the reminder");
        }
    }

    /// <summary>
    /// Sends advance reminders for all eligible schedules
    /// </summary>
    [HttpPost("send-advance-reminders")]
    public async Task<ActionResult<object>> SendAdvanceReminders()
    {
        try
        {
            var sentCount = await _scheduleService.SendAdvanceRemindersAsync();
            return Ok(new { message = $"Sent {sentCount} advance reminders", count = sentCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending advance reminders");
            return StatusCode(500, "An error occurred while sending advance reminders");
        }
    }

    #endregion

    #region Usage-Based Trigger Endpoints

    /// <summary>
    /// Gets schedules due based on usage triggers
    /// </summary>
    [HttpGet("due-by-usage")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetSchedulesDueByUsage()
    {
        try
        {
            var schedules = await _scheduleService.GetSchedulesDueByUsageAsync();
            return Ok(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules due by usage");
            return StatusCode(500, "An error occurred while retrieving schedules due by usage");
        }
    }

    /// <summary>
    /// Evaluates usage triggers for a specific schedule
    /// </summary>
    [HttpPost("{id:guid}/evaluate-usage-triggers")]
    public async Task<ActionResult<object>> EvaluateUsageTriggers(Guid id)
    {
        try
        {
            var triggerMet = await _scheduleService.EvaluateUsageTriggersAsync(id);
            return Ok(new { scheduleId = id, triggerMet = triggerMet });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating usage triggers for schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while evaluating usage triggers");
        }
    }

    /// <summary>
    /// Updates asset usage metrics
    /// </summary>
    [HttpPost("assets/{assetId:guid}/update-usage")]
    public async Task<IActionResult> UpdateAssetUsage(Guid assetId, [FromBody] UpdateAssetUsageDto updateDto)
    {
        try
        {
            await _scheduleService.UpdateAssetUsageAsync(assetId, updateDto.Mileage, updateDto.OperatingHours);
            return Ok(new { message = "Asset usage updated successfully", assetId = assetId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset usage for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while updating asset usage");
        }
    }

    #endregion

    #region Condition-Based Trigger Endpoints

    /// <summary>
    /// Gets schedules due based on condition triggers
    /// </summary>
    [HttpGet("due-by-condition")]
    public async Task<ActionResult<IEnumerable<MaintenanceScheduleDto>>> GetSchedulesDueByCondition()
    {
        try
        {
            var schedules = await _scheduleService.GetSchedulesDueByConditionAsync();
            return Ok(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedules due by condition");
            return StatusCode(500, "An error occurred while retrieving schedules due by condition");
        }
    }

    /// <summary>
    /// Evaluates condition triggers for a specific schedule
    /// </summary>
    [HttpPost("{id:guid}/evaluate-condition-triggers")]
    public async Task<ActionResult<object>> EvaluateConditionTriggers(Guid id)
    {
        try
        {
            var triggerMet = await _scheduleService.EvaluateConditionTriggersAsync(id);
            return Ok(new { scheduleId = id, triggerMet = triggerMet });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition triggers for schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while evaluating condition triggers");
        }
    }

    #endregion

    #region Multi-Criteria Evaluation Endpoints

    /// <summary>
    /// Evaluates if a work order should be generated for a schedule
    /// </summary>
    [HttpPost("{id:guid}/should-generate-work-order")]
    public async Task<ActionResult<object>> ShouldGenerateWorkOrder(Guid id)
    {
        try
        {
            var shouldGenerate = await _scheduleService.ShouldGenerateWorkOrderAsync(id);
            return Ok(new { scheduleId = id, shouldGenerateWorkOrder = shouldGenerate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating work order generation for schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while evaluating work order generation");
        }
    }

    /// <summary>
    /// Processes all usage-based schedules
    /// </summary>
    [HttpPost("process-usage-based")]
    public async Task<IActionResult> ProcessUsageBasedSchedules()
    {
        try
        {
            await _scheduleService.ProcessUsageBasedSchedulesAsync();
            return Ok(new { message = "Usage-based schedules processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing usage-based schedules");
            return StatusCode(500, "An error occurred while processing usage-based schedules");
        }
    }

    /// <summary>
    /// Processes all condition-based schedules
    /// </summary>
    [HttpPost("process-condition-based")]
    public async Task<IActionResult> ProcessConditionBasedSchedules()
    {
        try
        {
            await _scheduleService.ProcessConditionBasedSchedulesAsync();
            return Ok(new { message = "Condition-based schedules processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing condition-based schedules");
            return StatusCode(500, "An error occurred while processing condition-based schedules");
        }
    }

    #endregion
}

/// <summary>
/// DTO for updating asset usage
/// </summary>
public class UpdateAssetUsageDto
{
    public double? Mileage { get; set; }
    public double? OperatingHours { get; set; }
}
