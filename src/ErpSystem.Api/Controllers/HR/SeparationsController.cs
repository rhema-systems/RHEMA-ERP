using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The exit register (area 9b) — FRD §A1.10 <i>Separation, Clearance &amp; Exit</i> and §3.A.2
/// <i>Separation &amp; Final Settlement</i>.
/// </summary>
/// <remarks>
/// <para>One record per exit, whatever the route. Before this controller the only way out of the
/// organisation was a disciplinary case, so resignation, retirement, contract expiry and death had
/// enum members and no route. The disciplinary route writes here too — area 9 keeps owning the
/// decision and the hearing; the exit itself lives here, so the register, the clearance run and the
/// settlement are the same for a dismissal as for a resignation.</para>
///
/// <para><b>Every action carries its own gate.</b> There is no class-level <c>[Authorize]</c>,
/// because later slices must be able to open individual actions to the MD (FR-HR-092) and to
/// Internal Audit (FR-HR-185), and stacked <c>[Authorize]</c> attributes are ANDed — a class-level
/// role gate would keep applying however the method were marked. The cost of that choice is that a
/// new endpoint added here with no attribute is an <b>open</b> endpoint. Add the gate first.</para>
///
/// <para><b>There is deliberately no by-number lookup.</b> Separation numbers are sequential, so a
/// by-number endpoint would let anyone walk the range and harvest who is leaving — which is exactly
/// what an exit register must not leak. The number is a display and search field; lookups are by
/// id.</para>
/// </remarks>
[ApiController]
[Route("api/hr/separations")]
public class SeparationsController : ControllerBase
{
    private readonly ISeparationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SeparationsController> _logger;

    public SeparationsController(
        ISeparationService service,
        ICurrentUserService currentUser,
        ILogger<SeparationsController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// The acting employee, or null when the caller's login is not linked to an employee record.
    /// </summary>
    /// <remarks>
    /// Null rather than a throw: an HR officer whose user account has no employee link must still
    /// be able to raise a separation for somebody else, and recording <c>Guid.Empty</c> as the
    /// initiator would be a lie the audit trail cannot tell apart from a real one. The service
    /// distinguishes "nobody was named" from "the system raised it" via
    /// <c>IsSystemInitiated</c>.
    /// </remarks>
    private Guid? ActorEmployeeId() => _currentUser.EmployeeId;

    private ActionResult ToClientError(Exception ex) => ex switch
    {
        ArgumentException => NotFound(new { message = ex.Message }),
        InvalidOperationException => BadRequest(new { message = ex.Message }),
        _ => BadRequest(new { message = ex.Message }),
    };

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>The exit register, filtered and paged.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeSeparationListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeSeparationListDto>>> GetPaged(
        [FromQuery] EmployeeSeparationQueryDto query, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(query, cancellationToken));

    /// <summary>One separation in full.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>Every separation on record for one employee, newest first.</summary>
    [Authorize(Policy = HrPermissions.SeparationReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSeparationListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeSeparationListDto>>> GetForEmployee(
        Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest(new { message = "Invalid employee id." });
        return Ok(await _service.GetForEmployeeAsync(employeeId, cancellationToken));
    }

    // ── Writes ────────────────────────────────────────────────────────────────

    /// <summary>Raise a separation. It starts as a draft; nothing is decided by creating it.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Create(
        [FromBody] CreateEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (dto == null) return BadRequest(new { message = "A separation payload is required." });

        try
        {
            var created = await _service.CreateAsync(dto, ActorEmployeeId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Amend a draft separation.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Update(
        Guid id, [FromBody] UpdateEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "An update payload is required." });

        try
        {
            return Ok(await _service.UpdateAsync(id, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Withdraw a separation — a resignation retracted, a retirement deferred.</summary>
    [Authorize(Policy = HrPermissions.SeparationWritePolicy)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(EmployeeSeparationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSeparationDetailDto>> Cancel(
        Guid id, [FromBody] CancelEmployeeSeparationDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });
        if (dto == null) return BadRequest(new { message = "A cancellation reason is required." });

        try
        {
            return Ok(await _service.CancelAsync(id, dto, ActorEmployeeId(), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Delete a separation. Administration, not maintenance: a completed one is refused outright,
    /// because it is the record of somebody's exit and of what they were paid.
    /// </summary>
    [Authorize(Policy = HrPermissions.SeparationAdminPolicy)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest(new { message = "Invalid separation id." });

        try
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }
}
