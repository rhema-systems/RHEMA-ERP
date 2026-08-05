using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Pre-defined relievers configured on the employee profile; auto-populate leave plans/requests.
/// </summary>
[ApiController]
[Route("api/hr/employee-relievers")]
[Authorize]
public class EmployeeRelieversController : ControllerBase
{
    private readonly IEmployeeRelieverService _service;

    public EmployeeRelieversController(IEmployeeRelieverService service)
    {
        _service = service;
    }

    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeRelieverDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeRelieverDto>>> GetForEmployee(
        Guid employeeId, [FromQuery] bool activeOnly = false)
        => Ok(await _service.GetForEmployeeAsync(employeeId, activeOnly));

    [HttpPost]
    [ProducesResponseType(typeof(EmployeeRelieverDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeRelieverDto>> Create([FromBody] CreateEmployeeRelieverDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetForEmployee), new { employeeId = created.EmployeeId }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeRelieverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRelieverDto>> Update(Guid id, [FromBody] UpdateEmployeeRelieverDto dto)
    {
        try { return Ok(await _service.UpdateAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try { await _service.DeleteAsync(id); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }
}
