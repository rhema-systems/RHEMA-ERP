using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The pre-defined reliever roster: who covers for an employee, and in what order.
/// </summary>
/// <remarks>
/// <para>Read by the leave request form, which fills its first and second reliever slots from
/// priority 1 and 2. The roster is a <b>default source, never an override</b> — the reliever
/// recorded on a leave request stays authoritative for that request.</para>
///
/// <para>⚠ <b>Gated self-or-HR from areas 19-23 slice 7.</b> It carried a bare <c>[Authorize]</c>,
/// so any authenticated user could read <i>anyone's</i> roster and write one for <i>anyone</i> —
/// and a roster is a statement about who covers for whom, which is org-authority information about
/// identifiable people. An employee manages their own; HR and the admin roles manage anyone's.
/// This is the pattern the rest of HR uses: a role gate plus an ownership check, rather than a
/// per-area permission.</para>
/// </remarks>
[ApiController]
[Route("api/hr/employee-relievers")]
[Authorize]
public class EmployeeRelieversController : ControllerBase
{
    private readonly IEmployeeRelieverService _service;
    private readonly ILogger<EmployeeRelieversController> _logger;

    public EmployeeRelieversController(
        IEmployeeRelieverService service,
        ILogger<EmployeeRelieversController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>The caller's own employee id, or null when the account is not employee-linked.</summary>
    /// <remarks>
    /// ⚠ The claim is <c>employee_id</c>. <c>JwtTokenService</c> issues it under that name and nine
    /// call sites read it that way; a guessed <c>EmployeeId</c> parses to nothing, which would have
    /// silently forbidden every non-HR caller from their own roster.
    /// </remarks>
    private Guid? CurrentEmployeeId()
        => Guid.TryParse(User.FindFirst("employee_id")?.Value, out var id) && id != Guid.Empty ? id : null;

    private bool IsHrOrAdmin()
        => User.IsInRole(Constants.Roles.Hr)
           || User.IsInRole(Constants.Roles.TenantAdmin)
           || User.IsInRole(Constants.Roles.SuperAdmin);

    /// <summary>
    /// Whether the caller may see or change the roster belonging to <paramref name="employeeId"/>.
    /// </summary>
    /// <remarks>
    /// ⚠ An account with no employee link is not "everybody's owner" — it is nobody's. An HR actor
    /// passes on the role alone; anyone else must BE the employee whose roster it is.
    /// </remarks>
    private bool MayTouch(Guid employeeId)
        => IsHrOrAdmin() || (CurrentEmployeeId() is Guid me && me == employeeId);

    private IActionResult Forbidden()
        => StatusCode(StatusCodes.Status403Forbidden,
            new { message = "You may only view or change your own reliever roster." });

    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeRelieverDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetForEmployee(Guid employeeId, [FromQuery] bool activeOnly = false)
    {
        if (!MayTouch(employeeId))
            return Forbidden();

        try
        {
            return Ok(await _service.GetForEmployeeAsync(employeeId, activeOnly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the reliever roster for {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving the reliever roster.");
        }
    }

    /// <summary>
    /// The caller's own roster — what the leave request form reads.
    /// </summary>
    /// <remarks>
    /// Added in slice 7. The form needs "my relievers" and knew the employee id only because the
    /// screen happened to hold it; an endpoint that takes the actor from the token is one the client
    /// cannot get wrong, and it is the same rule the rest of HR follows.
    /// </remarks>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeRelieverDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMine([FromQuery] bool activeOnly = true)
    {
        if (CurrentEmployeeId() is not Guid me)
            return BadRequest(new { message = "Your user account is not linked to an employee record." });

        try
        {
            return Ok(await _service.GetForEmployeeAsync(me, activeOnly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the caller's reliever roster");
            return StatusCode(500, "An error occurred while retrieving the reliever roster.");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(EmployeeRelieverDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRelieverDto dto)
    {
        if (!MayTouch(dto.EmployeeId))
            return Forbidden();

        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetForEmployee), new { employeeId = created.EmployeeId }, created);
        }
        // An id that names nobody. Before slice 7 this reached the database and came back a 500.
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating a reliever row for {EmployeeId}", dto.EmployeeId);
            return StatusCode(500, "An error occurred while saving the reliever.");
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeRelieverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeRelieverDto dto)
    {
        // ⚠ The row is the subject here, not the body: the ownership question is "whose roster is
        // this row on", and only the row knows. Checking anything the caller sent would let the
        // caller answer it.
        var owner = await OwnerOfAsync(id);
        if (owner is null) return NotFound(new { message = $"Reliever setup '{id}' not found." });
        if (!MayTouch(owner.Value)) return Forbidden();

        try { return Ok(await _service.UpdateAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reliever row {Id}", id);
            return StatusCode(500, "An error occurred while saving the reliever.");
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var owner = await OwnerOfAsync(id);
        if (owner is null) return NotFound(new { message = $"Reliever setup '{id}' not found." });
        if (!MayTouch(owner.Value)) return Forbidden();

        try { await _service.DeleteAsync(id); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reliever row {Id}", id);
            return StatusCode(500, "An error occurred while removing the reliever.");
        }
    }

    /// <summary>Whose roster a row sits on, or null if there is no such row in this tenant.</summary>
    private async Task<Guid?> OwnerOfAsync(Guid id)
    {
        try { return await _service.GetOwnerEmployeeIdAsync(id); }
        catch (ArgumentException) { return null; }
    }
}
