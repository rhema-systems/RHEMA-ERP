using ErpSystem.Core.DTOs.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Leave encashment processing endpoints
/// </summary>
[ApiController]
[Route("api/hr/leave-encashments")]
[Authorize(Policy = "InternalOnly")]
public class LeaveEncashmentsController : ControllerBase
{
    private readonly ILeaveEncashmentService _service;
    private readonly ILogger<LeaveEncashmentsController> _logger;

    public LeaveEncashmentsController(ILeaveEncashmentService service, ILogger<LeaveEncashmentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LeaveEncashmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveEncashmentDto>>> GetAll(
        [FromQuery] int       year        = 0,
        [FromQuery] Guid?     employeeId  = null,
        [FromQuery] Guid?     leaveTypeId = null,
        [FromQuery] DateTime? from        = null,
        [FromQuery] DateTime? to          = null,
        [FromQuery] string?   search      = null)
    {
        if (year == 0) year = DateTime.Today.Year;
        return Ok(await _service.GetAllEncashmentsAsync(year, employeeId, leaveTypeId, from, to, search));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> GetById(Guid id)
    {
        try { return Ok(await _service.GetByIdAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LeaveEncashmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveEncashmentDto>>> GetByEmployee(
        Guid employeeId,
        [FromQuery] int year = 0)
    {
        if (year == 0) year = DateTime.Today.Year;
        return Ok(await _service.GetEmployeeEncashmentsAsync(employeeId, year));
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> Request([FromBody] CreateLeaveEncashmentDto dto)
    {
        try
        {
            var result = await _service.RequestEncashmentAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting leave encashment");
            return StatusCode(500, "An error occurred while requesting the encashment");
        }
    }

    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> Approve(Guid id)
    {
        try { return Ok(await _service.ApproveEncashmentAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> Reject(Guid id, [FromBody] string reason)
    {
        try { return Ok(await _service.RejectEncashmentAsync(id, reason)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/process")]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> MarkAsProcessed(Guid id, [FromBody] ProcessLeaveEncashmentDto dto)
    {
        try { return Ok(await _service.MarkAsProcessedAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking encashment {id} as processed", id);
            return StatusCode(500, "An error occurred while processing the encashment");
        }
    }
}
