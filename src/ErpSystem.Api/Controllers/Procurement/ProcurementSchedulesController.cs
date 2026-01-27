using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class ProcurementSchedulesController : ControllerBase
{
    private readonly IProcurementScheduleService _scheduleService;
    private readonly ILogger<ProcurementSchedulesController> _logger;

    public ProcurementSchedulesController(
        IProcurementScheduleService scheduleService,
        ILogger<ProcurementSchedulesController> logger)
    {
        _scheduleService = scheduleService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcurementScheduleDto>>> GetSchedules(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] Guid? departmentId = null, [FromQuery] Guid? planId = null)
    {
        try
        {
            var result = await _scheduleService.GetSchedulesAsync(page, pageSize, search, status, departmentId, planId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting procurement schedules");
            return StatusCode(500, "An error occurred while retrieving procurement schedules");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcurementScheduleDetailDto>> GetSchedule(Guid id)
    {
        try
        {
            var schedule = await _scheduleService.GetByIdAsync(id);
            if (schedule == null) return NotFound($"Schedule with ID {id} not found");
            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while retrieving the schedule");
        }
    }

    [HttpGet("plan/{planId}")]
    public async Task<ActionResult<IEnumerable<ProcurementScheduleDto>>> GetByPlanId(Guid planId)
    {
        try { return Ok(await _scheduleService.GetByPlanIdAsync(planId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving schedules");
        }
    }

    [HttpGet("department/{departmentId}")]
    public async Task<ActionResult<IEnumerable<ProcurementScheduleDto>>> GetByDepartment(Guid departmentId)
    {
        try { return Ok(await _scheduleService.GetByDepartmentAsync(departmentId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules for department {DepartmentId}", departmentId);
            return StatusCode(500, "An error occurred while retrieving schedules");
        }
    }

    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<ProcurementScheduleDto>>> GetByDateRange(
        [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try { return Ok(await _scheduleService.GetByDateRangeAsync(startDate, endDate)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules by date range");
            return StatusCode(500, "An error occurred while retrieving schedules");
        }
    }

    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<ProcurementScheduleDto>>> GetUpcoming([FromQuery] int days = 30)
    {
        try { return Ok(await _scheduleService.GetUpcomingSchedulesAsync(days)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upcoming schedules");
            return StatusCode(500, "An error occurred while retrieving upcoming schedules");
        }
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<ProcurementScheduleDto>>> GetOverdue()
    {
        try { return Ok(await _scheduleService.GetOverdueSchedulesAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue schedules");
            return StatusCode(500, "An error occurred while retrieving overdue schedules");
        }
    }

    [HttpGet("consolidation-opportunities")]
    public async Task<ActionResult<IEnumerable<ProcurementScheduleDto>>> GetConsolidationOpportunities()
    {
        try { return Ok(await _scheduleService.GetConsolidationOpportunitiesAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting consolidation opportunities");
            return StatusCode(500, "An error occurred while retrieving consolidation opportunities");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProcurementScheduleDetailDto>> CreateSchedule([FromBody] CreateProcurementScheduleDto dto)
    {
        try
        {
            var schedule = await _scheduleService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule");
            return StatusCode(500, "An error occurred while creating the schedule");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProcurementScheduleDetailDto>> UpdateSchedule(Guid id, [FromBody] CreateProcurementScheduleDto dto)
    {
        try { return Ok(await _scheduleService.UpdateAsync(id, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while updating the schedule");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteSchedule(Guid id)
    {
        try { await _scheduleService.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while deleting the schedule");
        }
    }

    [HttpPost("{id}/start")]
    public async Task<ActionResult<ProcurementScheduleDetailDto>> StartSchedule(Guid id)
    {
        try { return Ok(await _scheduleService.StartScheduleAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while starting the schedule");
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<ActionResult<ProcurementScheduleDetailDto>> CompleteSchedule(Guid id)
    {
        try { return Ok(await _scheduleService.CompleteScheduleAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while completing the schedule");
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<ProcurementScheduleDetailDto>> CancelSchedule(Guid id, [FromBody] string reason)
    {
        try { return Ok(await _scheduleService.CancelScheduleAsync(id, reason)); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling schedule {ScheduleId}", id);
            return StatusCode(500, "An error occurred while cancelling the schedule");
        }
    }
}