using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The staff directory and "my team", as an employee uses them (area 25 slice 13a).
/// </summary>
/// <remarks>
/// <para><b>Deliberately open to any linked internal user, with a lean projection.</b> W3 slice 11
/// already settled that lean directory reads stay ungated — the shared <c>EmployeePicker</c> rides
/// <c>POST api/hr/employees/paged</c>, which any internal caller may hit — so gating this would
/// gate the front door while leaving the back one open. What this controller adds is the lean
/// part: <see cref="StaffDirectoryEntryDto"/> projects name, role, unit, location and work email
/// and nothing else, where the employee summary it could have reused carries gender, employment
/// type, hire date, years of service and expatriate status.</para>
///
/// <para><b>Nothing here is id-bearing in the dangerous sense.</b> The one route that takes an
/// employee id returns the same public card for anybody, so there is no self-arm to get wrong;
/// the personal reads still live behind <c>HR.Employee.Read</c> on <c>EmployeesController</c>.
/// <c>my-team</c> takes the caller from the token and has no id-bearing twin at all.</para>
/// </remarks>
[ApiController]
[Route("api/employee-portal")]
[Authorize(Policy = "InternalOnly")]
public class StaffDirectoryController : ControllerBase
{
    private readonly IStaffDirectoryService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffDirectoryController(IStaffDirectoryService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private IActionResult NoEmployee() =>
        Problem(
            detail:     "Your account is not linked to an employee record. Please contact HR.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    /// <summary>
    /// Search or browse the directory. Everyone on strength, paged.
    /// </summary>
    /// <remarks>
    /// <c>organizationUnitId</c> browses the unit AND everything beneath it. Units on DEFAULT hold
    /// their people in child units: measured 2026-08-27, "Finance Department" browses to 7,897
    /// people of whom 7,896 sit in its child "Financial Accounts" — one single person is a direct
    /// member. A direct-membership browse of Finance would therefore have found one person out of
    /// nearly eight thousand.
    /// </remarks>
    [HttpGet("directory")]
    [ProducesResponseType(typeof(PagedResult<StaffDirectoryEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDirectory(
        [FromQuery] string? search = null,
        [FromQuery] Guid? organizationUnitId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid me) return NoEmployee();

        // A SUPPLIED empty guid is a bad request, not "no filter". The probe caught this:
        // `organizationUnitId=99999999-…` (a unit that does not exist) correctly returned 0 rows,
        // while `organizationUnitId=00000000-…` returned all 8,007 — the same question asked two
        // ways, and the emptier-looking one silently widened the read to the whole tenant instead
        // of narrowing it. A filter that can be ignored without saying so is a filter that lies.
        if (organizationUnitId == Guid.Empty && Request.Query.ContainsKey(nameof(organizationUnitId)))
            return BadRequest("Invalid organization unit id.");
        if (locationId == Guid.Empty && Request.Query.ContainsKey(nameof(locationId)))
            return BadRequest("Invalid location id.");

        return Ok(await _service.SearchAsync(me, search, organizationUnitId, locationId, page, pageSize, ct));
    }

    /// <summary>One colleague's card: their role, their unit path, their reporting line.</summary>
    /// <remarks>
    /// A person who is not on strength in this tenant is a lookup miss, not a refusal — the
    /// directory has nothing to hide about who is in it, and 404 is the honest answer for
    /// someone who is not.
    /// </remarks>
    [HttpGet("directory/{id:guid}")]
    [ProducesResponseType(typeof(StaffDirectoryProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDirectoryEntry(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid me) return NoEmployee();

        var profile = await _service.GetProfileAsync(me, id, ct);
        return profile == null ? NotFound() : Ok(profile);
    }

    /// <summary>Who the caller reports to, and who reports to them.</summary>
    /// <remarks>
    /// Honestly empty for most people: 416 of 8,131 live employees carry a <c>ManagerId</c> on
    /// DEFAULT (5.1%), so the reports list is usually blank and the manager line usually null.
    /// The screen says so rather than pretending the feature is broken — the org-authority model
    /// that would populate this is a deferred module.
    /// </remarks>
    [HttpGet("my-team")]
    [ProducesResponseType(typeof(MyTeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyTeam(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid me) return NoEmployee();
        return Ok(await _service.GetMyTeamAsync(me, ct));
    }
}
