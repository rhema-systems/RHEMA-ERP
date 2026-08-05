using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Position-level and employee-level pay-component assignments, plus the consolidated employee
/// emolument roll-up and the derived leave-encashment per-day rate (Phase 4).
/// </summary>
[ApiController]
[Route("api/hr/emoluments")]
[Authorize]
public class EmolumentsController : ControllerBase
{
    private readonly IEmolumentService _service;
    private readonly ILogger<EmolumentsController> _logger;

    public EmolumentsController(IEmolumentService service, ILogger<EmolumentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ─── Position-level assignments ────────────────────────────────────────────

    [HttpGet("positions/{positionId:guid}/components")]
    [ProducesResponseType(typeof(IEnumerable<PositionPayComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PositionPayComponentDto>>> GetPositionComponents(Guid positionId)
        => Ok(await _service.GetPositionComponentsAsync(positionId));

    [HttpPost("positions/components")]
    [ProducesResponseType(typeof(PositionPayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PositionPayComponentDto>> AssignPositionComponent([FromBody] CreatePositionPayComponentDto dto)
    {
        try { return Ok(await _service.AssignPositionComponentAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("positions/components/{id:guid}")]
    [ProducesResponseType(typeof(PositionPayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionPayComponentDto>> UpdatePositionComponent(
        Guid id, [FromBody] UpdatePositionPayComponentRequest dto)
    {
        try { return Ok(await _service.UpdatePositionComponentAsync(id, dto.Amount, dto.IsActive)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("positions/components/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePositionComponent(Guid id)
    {
        try { await _service.RemovePositionComponentAsync(id); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Employee-level assignments ────────────────────────────────────────────

    [HttpGet("employees/{employeeId:guid}/components")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePayComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePayComponentDto>>> GetEmployeeComponents(Guid employeeId)
        => Ok(await _service.GetEmployeeComponentsAsync(employeeId));

    [HttpPost("employees/components")]
    [ProducesResponseType(typeof(EmployeePayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePayComponentDto>> AssignEmployeeComponent([FromBody] CreateEmployeePayComponentDto dto)
    {
        try { return Ok(await _service.AssignEmployeeComponentAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("employees/components/{id:guid}")]
    [ProducesResponseType(typeof(EmployeePayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePayComponentDto>> UpdateEmployeeComponent(
        Guid id, [FromBody] UpdateEmployeePayComponentDto dto)
    {
        try { return Ok(await _service.UpdateEmployeeComponentAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("employees/components/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEmployeeComponent(Guid id)
    {
        try { await _service.RemoveEmployeeComponentAsync(id); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Consolidated / derived ────────────────────────────────────────────────

    [HttpGet("employees/{employeeId:guid}/summary")]
    [ProducesResponseType(typeof(EmployeeEmolumentSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmolumentSummaryDto>> GetEmployeeSummary(
        Guid employeeId, [FromQuery] DateOnly? asOf = null)
    {
        try { return Ok(await _service.GetEmployeeEmolumentSummaryAsync(employeeId, asOf)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("encashment-rate")]
    [ProducesResponseType(typeof(EncashmentRateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EncashmentRateResult>> GetEncashmentRate(
        [FromQuery] Guid employeeId, [FromQuery] Guid leaveTypeId,
        [FromQuery] DateOnly? asOf = null, [FromQuery] decimal days = 0)
    {
        try
        {
            var rate = await _service.GetEncashmentDailyRateAsync(
                employeeId, leaveTypeId, asOf ?? DateOnly.FromDateTime(DateTime.UtcNow));
            return Ok(new EncashmentRateResult
            {
                DailyRate = rate,
                Days = days,
                Amount = Math.Round(rate * days, 2)
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

public class UpdatePositionPayComponentRequest
{
    public decimal? Amount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EncashmentRateResult
{
    public decimal DailyRate { get; set; }
    public decimal Days { get; set; }
    public decimal Amount { get; set; }
}
