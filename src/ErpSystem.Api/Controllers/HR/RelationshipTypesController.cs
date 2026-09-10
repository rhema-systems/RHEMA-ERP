using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// How one person is tied to another — "Spouse", "Former manager", "Landlord" — as the tenant's own
/// maintained list rather than as free text on every record that asks.
/// </summary>
/// <remarks>
/// <para>Round 2, lane D2 (register rows E-11a, E-11b). Four columns spelt this out separately and
/// never agreed: the employee referee, the guarantor, the next of kin and the candidate referee.</para>
///
/// <para>⚠ <b>Dependants are NOT a consumer.</b> They keep the <c>DependentRelationship</c> enum,
/// because benefit eligibility branches on its members and <c>ExpatriateFamilyMember</c> reuses it
/// deliberately. See the entity for the full reasoning.</para>
///
/// <para>Gating follows every other People Reference Data lookup: reading is part of filling in an
/// employee record, so <c>HR.Employee.Read</c>; changing the vocabulary is <c>HR.Employee.Write</c>;
/// deleting outright is <c>HR.Employee.Admin</c>, because it is the only withdrawal that cannot be
/// undone.</para>
/// </remarks>
[ApiController]
[Route("api/hr/relationship-types")]
[Authorize(Policy = "InternalOnly")]
public class RelationshipTypesController : ControllerBase
{
    private readonly IRelationshipTypeService _service;
    private readonly ILogger<RelationshipTypesController> _logger;

    public RelationshipTypesController(
        IRelationshipTypeService service,
        ILogger<RelationshipTypesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// The relationship types. A PICKER passes <c>activeOnly=true</c> and the categories its screen
    /// accepts; the master screen passes neither and sees everything, retired rows included.
    /// </summary>
    /// <remarks>
    /// ⚠ <paramref name="categories"/> is a CONVENIENCE for the screens, not the rule. The rule is
    /// enforced on the write, in the service that stores the id — a filter a caller can simply not
    /// send would protect nothing.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<RelationshipTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RelationshipTypeDto>>> GetAll(
        [FromQuery] bool activeOnly = false,
        [FromQuery] RelationshipCategory[]? categories = null,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(activeOnly, categories, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(RelationshipTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RelationshipTypeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(RelationshipTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RelationshipTypeDto>> Create(
        [FromBody] CreateRelationshipTypeDto dto, CancellationToken cancellationToken)
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
    [ProducesResponseType(typeof(RelationshipTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RelationshipTypeDto>> Update(
        Guid id, [FromBody] UpdateRelationshipTypeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, cancellationToken));
        }
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
        try
        {
            await _service.DeactivateAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Deletes a value outright. Refused with a count while any record still names it.
    /// </summary>
    /// <remarks>
    /// ⚠ 422 rather than 400, matching the geography module's delete guard: the request is
    /// well-formed and the caller is entitled to make it — the data simply will not allow it yet,
    /// and the message says exactly how many records stand in the way.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }
}
