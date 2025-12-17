using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/staff-schedules")]
[Authorize]
public class MaintenanceStaffScheduleController : ControllerBase
{
    private readonly IMaintenanceStaffScheduleService _scheduleService;
    private readonly ILogger<MaintenanceStaffScheduleController> _logger;

    public MaintenanceStaffScheduleController(
        IMaintenanceStaffScheduleService scheduleService,
        ILogger<MaintenanceStaffScheduleController> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateMaintenanceStaffScheduleDto createDto)
    {
        try
        {
            var result = await _scheduleService.CreateScheduleAsync(createDto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating staff schedule");
            return BadRequest(new { message = "Failed to create schedule", error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] UpdateMaintenanceStaffScheduleDto updateDto)
    {
        try
        {
            var result = await _scheduleService.UpdateScheduleAsync(id, updateDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", id);
            return BadRequest(new { message = "Failed to update schedule", error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        try
        {
            await _scheduleService.DeleteScheduleAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", id);
            return BadRequest(new { message = "Failed to delete schedule", error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetScheduleById(Guid id)
    {
        try
        {
            var result = await _scheduleService.GetScheduleByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule {ScheduleId}", id);
            return BadRequest(new { message = "Failed to get schedule", error = ex.Message });
        }
    }

    [HttpGet("by-technician/{technicianId}")]
    public async Task<IActionResult> GetSchedulesByTechnician(Guid technicianId)
    {
        try
        {
            var results = await _scheduleService.GetSchedulesByTechnicianIdAsync(technicianId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules for technician {TechnicianId}", technicianId);
            return BadRequest(new { message = "Failed to get schedules", error = ex.Message });
        }
    }

    [HttpGet("by-workorder/{workOrderId}")]
    public async Task<IActionResult> GetSchedulesByWorkOrder(Guid workOrderId)
    {
        try
        {
            var results = await _scheduleService.GetSchedulesByWorkOrderIdAsync(workOrderId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules for work order {WorkOrderId}", workOrderId);
            return BadRequest(new { message = "Failed to get schedules", error = ex.Message });
        }
    }

    [HttpGet("by-date-range")]
    public async Task<IActionResult> GetSchedulesByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            var results = await _scheduleService.GetSchedulesByDateRangeAsync(startDate, endDate);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules by date range");
            return BadRequest(new { message = "Failed to get schedules", error = ex.Message });
        }
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartSchedule(Guid id)
    {
        try
        {
            var result = await _scheduleService.StartScheduleAsync(id);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting schedule {ScheduleId}", id);
            return BadRequest(new { message = "Failed to start schedule", error = ex.Message });
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteSchedule(Guid id, [FromBody] int? actualTravelMinutes = null)
    {
        try
        {
            var result = await _scheduleService.CompleteScheduleAsync(id, actualTravelMinutes);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing schedule {ScheduleId}", id);
            return BadRequest(new { message = "Failed to complete schedule", error = ex.Message });
        }
    }
}
