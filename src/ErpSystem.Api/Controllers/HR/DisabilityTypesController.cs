using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The tenant's disability catalogue — what the employee form and the dependant form pick from
/// once the disability box is ticked (round 3, lane P2; register row E-5).
/// </summary>
/// <remarks>
/// Gating follows every other People Reference Data lookup: reading is part of filling in an
/// employee record, so <c>HR.Employee.Read</c>; changing the vocabulary is <c>HR.Employee.Write</c>;
/// deleting outright is <c>HR.Employee.Admin</c>, because it is the only withdrawal that cannot be
/// undone — and it is refused with a count while any record names the row.
/// </remarks>
[ApiController]
[Route("api/hr/disability-types")]
[Authorize(Policy = "InternalOnly")]
public class DisabilityTypesController : ControllerBase
{
    private readonly IDisabilityTypeService _service;

    public DisabilityTypesController(IDisabilityTypeService service)
    {
        _service = service;
    }

    /// <summary>The catalogue. A picker passes <c>activeOnly=true</c>; the master screen passes nothing and sees retired rows too.</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<DisabilityTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DisabilityTypeDto>>> GetAll(
        [FromQuery] bool activeOnly = false,
        [FromQuery] DisabilityCategory[]? categories = null,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(activeOnly, categories, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(DisabilityTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisabilityTypeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(DisabilityTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DisabilityTypeDto>> Create([FromBody] CreateDisabilityTypeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(DisabilityTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisabilityTypeDto>> Update(Guid id, [FromBody] UpdateDisabilityTypeDto dto, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpdateAsync(id, dto, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Retires a value: it stops being offered, and every record already naming it keeps it.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        try { await _service.DeactivateAsync(id, cancellationToken); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Deletes a value outright. 422 with a count while any employee or dependant still names it.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try { await _service.DeleteAsync(id, cancellationToken); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }
}
