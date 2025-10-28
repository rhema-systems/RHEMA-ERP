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

}