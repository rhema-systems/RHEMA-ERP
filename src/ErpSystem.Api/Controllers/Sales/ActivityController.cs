using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/activities")]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activityService;

    public ActivityController(IActivityService activityService)
    {
        _activityService = activityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? activityType = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? assignedToId = null,
        [FromQuery] Guid? leadId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? opportunityId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _activityService.GetAllAsync(page, pageSize, search, activityType, status, assignedToId, leadId, customerId, opportunityId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var activity = await _activityService.GetByIdAsync(id);
        return activity == null ? NotFound() : Ok(activity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityDto dto)
    {
        var activity = await _activityService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = activity.Id }, activity);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateActivityDto dto)
    {
        var activity = await _activityService.UpdateAsync(id, dto);
        return Ok(activity);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromQuery] string? outcome = null, [FromQuery] string? notes = null)
    {
        var activity = await _activityService.CompleteAsync(id, outcome, notes);
        return Ok(activity);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] string? reason = null)
    {
        var activity = await _activityService.CancelAsync(id, reason);
        return Ok(activity);
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] Guid? leadId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? opportunityId = null)
    {
        var timeline = await _activityService.GetTimelineAsync(leadId, customerId, opportunityId);
        return Ok(timeline);
    }

    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcoming([FromQuery] int daysAhead = 7, [FromQuery] Guid? assignedToId = null)
    {
        var activities = await _activityService.GetUpcomingAsync(daysAhead, assignedToId);
        return Ok(activities);
    }

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue([FromQuery] Guid? assignedToId = null)
    {
        var activities = await _activityService.GetOverdueAsync(assignedToId);
        return Ok(activities);
    }
}
