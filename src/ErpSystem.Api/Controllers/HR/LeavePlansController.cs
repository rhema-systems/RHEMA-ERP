using ErpSystem.Core.DTOs.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Leave plan management endpoints
/// </summary>
[ApiController]
[Route("api/hr/leave-plans")]
[Authorize]
public class LeavePlansController : ControllerBase
{
    private readonly ILeavePlanService _service;
    private readonly ILogger<LeavePlansController> _logger;

    public LeavePlansController(ILeavePlanService service, ILogger<LeavePlansController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LeavePlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeavePlanDto>>> GetByEmployee(
        Guid employeeId,
        [FromQuery] int year = 0)
    {
        if (year == 0) year = DateTime.Today.Year;
        return Ok(await _service.GetByEmployeeAndYearAsync(employeeId, year));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LeavePlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeavePlanDto>>> GetByYear([FromQuery] int year = 0)
    {
        if (year == 0) year = DateTime.Today.Year;
        return Ok(await _service.GetByYearAsync(year));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> GetById(Guid id)
    {
        try { return Ok(await _service.GetByIdAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeavePlanDto>> Create([FromBody] CreateLeavePlanDto dto)
    {
        try
        {
            var result = await _service.CreateLeavePlanAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating leave plan");
            return StatusCode(500, "An error occurred while creating the leave plan");
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Update(Guid id, [FromBody] CreateLeavePlanDto dto)
    {
        try { return Ok(await _service.UpdateLeavePlanAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating leave plan {id}", id);
            return StatusCode(500, "An error occurred while updating the leave plan");
        }
    }

    [HttpPatch("{id:guid}/submit")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Submit(Guid id)
    {
        try { return Ok(await _service.SubmitLeavePlanAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Approve(Guid id)
    {
        try { return Ok(await _service.ApproveLeavePlanAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Reject(Guid id, [FromBody] string reason)
    {
        try { return Ok(await _service.RejectLeavePlanAsync(id, reason)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/suggest-changes")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> SuggestChanges(Guid id, [FromBody] SuggestLeavePlanChangesDto dto)
    {
        try { return Ok(await _service.SuggestChangesAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/respond-suggestion")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> RespondToSuggestion(Guid id, [FromBody] RespondToLeaveSuggestionDto dto)
    {
        try { return Ok(await _service.RespondToSuggestionAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            await _service.CancelLeavePlanAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
