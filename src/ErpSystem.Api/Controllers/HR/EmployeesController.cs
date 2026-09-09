using ErpSystem.Core.DTOs.HR;
// Payroll's profile DTO, read through HR's door (lane E1). HR reads payroll's types; it does not edit them.
using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _service;
    private readonly IPayrollMembershipService _payrollMembership;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(
        IEmployeeService service,
        IPayrollMembershipService payrollMembership,
        ICurrentUserService currentUserService,
        ILogger<EmployeesController> logger)
    {
        _service = service;
        _payrollMembership = payrollMembership;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region CRUD

    // MODIFIED ENDPOINT: now delegates to intent-based read service; returns 404 when missing.
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto?>> GetEmployee(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        var result = await _service.GetEmployeeSummaryByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    // MODIFIED ENDPOINT: now delegates to intent-based read service; returns 404 when missing.
    [HttpGet("{id:guid}/details")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto?>> GetEmployeeDetails(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        var result = await _service.GetEmployeeDetailsByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    // NEW ENDPOINT: full profile (HR 360) read model.
    [HttpGet("{id:guid}/profile")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeFullProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeFullProfileDto?>> GetEmployeeFullProfile(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        var result = await _service.GetEmployeeFullProfileByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    // MODIFIED ENDPOINT: now delegates to intent-based read service; returns 404 when missing.
    [HttpGet("number/{employeeNumber}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto?>> GetByEmployeeNumber(string employeeNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return BadRequest("Employee number is required.");

        var result = await _service.GetEmployeeSummaryByEmployeeNumberAsync(employeeNumber, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    // NEW ENDPOINT: details read by employee number.
    [HttpGet("number/{employeeNumber}/details")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto?>> GetEmployeeDetailsByEmployeeNumber(string employeeNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return BadRequest("Employee number is required.");

        var result = await _service.GetEmployeeDetailsByEmployeeNumberAsync(employeeNumber, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    // NEW ENDPOINT: full profile read by employee number.
    [HttpGet("number/{employeeNumber}/profile")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeFullProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeFullProfileDto?>> GetEmployeeFullProfileByEmployeeNumber(string employeeNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return BadRequest("Employee number is required.");

        var result = await _service.GetEmployeeFullProfileByEmployeeNumberAsync(employeeNumber, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    // NEW ENDPOINT: summary read by email.
    [HttpGet("email/{email}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto?>> GetByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email is required.");

        var result = await _service.GetEmployeeSummaryByEmailAsync(email, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    // MODIFIED ENDPOINT: now returns the canonical details model (no legacy summary create).
    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDetailDto>> CreateEmployee([FromBody] CreateEmployeeDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await _service.CreateEmployeeAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetEmployeeDetails), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Records an employee who already exists elsewhere, keeping the staff number they came with.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Not a variant of create — a different act.</b> The normal create REFUSES a
    /// supplied number on an auto-numbered register, because a hand-typed value can occupy a number
    /// the sequence is about to issue. An existing employee's number is not ours to reissue: it is
    /// on their ID card and referenced by payroll, so this path requires it and honours it.</para>
    ///
    /// <para><b>And it teaches the counter.</b> A register loaded without this endpoint leaves the
    /// counter at zero while thousands of numbers are in use, and the first hire afterwards is
    /// handed one of them — surfacing as a unique-index violation on an unrelated screen. Where a
    /// load has already happened by other means, the settings screen's counter reconcile repairs
    /// the same thing after the fact.</para>
    ///
    /// <para>Admin-gated rather than Write-gated: accepting numbers as given bypasses the rule that
    /// keeps the register consistent, which is a different privilege from adding a new hire.</para>
    /// </remarks>
    [HttpPost("import")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDetailDto>> ImportEmployee([FromBody] CreateEmployeeDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await _service.ImportEmployeeAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetEmployeeDetails), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // MODIFIED ENDPOINT: now returns the canonical details model (no legacy summary update).
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto>> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await _service.UpdateEmployeeAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // MODIFIED ENDPOINT: now returns 404 when employee not found (instead of always 204).
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmployee(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        var existing = await _service.GetEmployeeSummaryByIdAsync(id, cancellationToken);
        if (existing == null) return NotFound();

        await _service.DeleteEmployeeAsync(id, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Status & Lifecycle

    // MODIFIED ENDPOINT: now delegates to lifecycle service; keeps legacy response shape.
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateEmployee(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        try
        {
            await _service.DeactivateEmployeeAsync(id, cancellationToken);
            return Ok(new { message = "Employee deactivated" });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // MODIFIED ENDPOINT: now delegates to lifecycle service; keeps legacy response shape.
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateEmployee(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        try
        {
            await _service.ActivateEmployeeAsync(id, cancellationToken);
            return Ok(new { message = "Employee activated" });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // MODIFIED ENDPOINT: now delegates to lifecycle service; keeps legacy response shape.
    [HttpPost("{id:guid}/terminate")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TerminateEmployee(Guid id, [FromBody] TerminateEmployeeDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            await _service.TerminateEmployeeAsync(id, dto, cancellationToken);
            return Ok(new { message = "Employee terminated" });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINT: reinstate a terminated employee.
    [HttpPost("{id:guid}/reinstate")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReinstateEmployee(Guid id, [FromBody] ReinstateEmployeeRequest? request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        try
        {
            await _service.ReinstateEmployeeAsync(id, request?.Notes, cancellationToken);
            return Ok(new { message = "Employee reinstated" });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    #endregion

    #region Filtering

    // NEW ENDPOINT: paged employees for Blazor grids / server-side paging.
    [HttpPost("paged")]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetPaged([FromBody] EmployeeSearchDto search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _service.GetEmployeesPagedAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    // NEW ENDPOINT: filter by organization unit (new org model).
    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetByOrganizationUnit(Guid organizationUnitId, CancellationToken cancellationToken)
    {
        if (organizationUnitId == Guid.Empty) return BadRequest("Invalid organization unit id.");
        return Ok(await _service.GetEmployeesByOrganizationUnitAsync(organizationUnitId, cancellationToken));
    }

    // NEW ENDPOINT: filter by organization level.
    [HttpGet("organization-level/{organizationLevelId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetByOrganizationLevel(Guid organizationLevelId, CancellationToken cancellationToken)
    {
        if (organizationLevelId == Guid.Empty) return BadRequest("Invalid organization level id.");
        return Ok(await _service.GetEmployeesByOrganizationLevelAsync(organizationLevelId, cancellationToken));
    }

    // NEW ENDPOINT: filter by location.
    [HttpGet("location/{locationId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetByLocation(Guid locationId, CancellationToken cancellationToken)
    {
        if (locationId == Guid.Empty) return BadRequest("Invalid location id.");
        return Ok(await _service.GetEmployeesByLocationAsync(locationId, cancellationToken));
    }

    // NEW ENDPOINT: direct reports for a manager.
    [HttpGet("manager/{managerId:guid}/direct-reports")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetDirectReports(Guid managerId, CancellationToken cancellationToken)
    {
        if (managerId == Guid.Empty) return BadRequest("Invalid manager id.");
        return Ok(await _service.GetDirectReportsAsync(managerId, cancellationToken));
    }

    /// <summary>
    /// The signed-in manager's own direct reports.
    /// </summary>
    /// <remarks>
    /// The token-derived twin of the route above, matching <c>manager/me/team-cycles</c> on the
    /// appraisals controller. Screens that need "my team" have no employee id of their own, and
    /// making them fetch one is how several of the module's authorization holes started — a client
    /// that must know its own id can pass someone else's.
    /// </remarks>
    [HttpGet("manager/me/direct-reports")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetMyDirectReports(CancellationToken cancellationToken)
    {
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty)
            return Forbid();

        return Ok(await _service.GetDirectReportsAsync(me, cancellationToken));
    }

    // NEW ENDPOINT: management chain for a given employee.
    [HttpGet("{employeeId:guid}/management-chain")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetManagementChain(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");

        try
        {
            var chain = await _service.GetManagementChainAsync(employeeId, cancellationToken);
            return Ok(chain);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    #endregion

    #region Maintenance Technicians

    [HttpGet("technicians")]
    public async Task<ActionResult<IEnumerable<MaintenanceTechnicianDto>>> GetTechnicians()
        => Ok(await _service.GetMaintenanceTechniciansAsync());

    [HttpGet("technicians/available")]
    public async Task<ActionResult<IEnumerable<MaintenanceTechnicianDto>>> GetAvailableTechnicians()
        => Ok(await _service.GetAvailableTechniciansAsync());

    [HttpGet("technicians/{employeeId:guid}")]
    public async Task<ActionResult<MaintenanceTechnicianDto?>> GetTechnician(Guid employeeId)
    {
        var result = await _service.GetTechnicianByIdAsync(employeeId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("technicians/{employeeId:guid}/availability")]
    public async Task<ActionResult<TechnicianAvailabilityDto?>> GetTechnicianAvailability(Guid employeeId)
    {
        var result = await _service.GetTechnicianAvailabilityAsync(employeeId);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("technicians/skill/{skillId:guid}")]
    public async Task<ActionResult<IEnumerable<MaintenanceTechnicianDto>>> GetTechniciansWithSkill(
        Guid skillId,
        [FromQuery] SkillLevel? minLevel = null)
        => Ok(await _service.GetTechniciansWithSkillAsync(skillId, minLevel));

    #endregion

    #region Stats

    [HttpGet("stats/total")]
    public async Task<ActionResult<int>> GetTotalEmployeeCount()
        => Ok(await _service.GetTotalEmployeeCountAsync());

    [HttpGet("stats/active")]
    public async Task<ActionResult<int>> GetActiveEmployeeCount()
        => Ok(await _service.GetActiveEmployeeCountAsync());

    [HttpGet("stats/by-status")]
    public async Task<ActionResult<Dictionary<StaffStatus, int>>> GetEmployeeCountByStatus()
        => Ok(await _service.GetEmployeeCountByStatusAsync());

    // `stats/by-department` was removed in slice 10. It grouped on the deprecated Department
    // dimension, had no caller in the solution, and `GET api/Organogram/units` already answers
    // headcount by organisation unit — with the subtree rollup this never had.

    #endregion

    #region Relationship subresources (Employee aggregate)

    // NEW ENDPOINTS: Emergency contacts
    [HttpGet("{employeeId:guid}/emergency-contacts")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeEmergencyContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeEmergencyContactDto>>> GetEmergencyContacts(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");

        try
        {
            return Ok(await _service.GetEmergencyContactsAsync(employeeId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/emergency-contacts")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> AddEmergencyContact(Guid employeeId, [FromBody] CreateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddEmergencyContactAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetEmergencyContacts), new { employeeId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> UpdateEmergencyContact(Guid employeeId, Guid emergencyContactId, [FromBody] UpdateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = emergencyContactId;

        try
        {
            var updated = await _service.UpdateEmergencyContactAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            var ok = await _service.RemoveEmergencyContactAsync(emergencyContactId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}/set-primary")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> SetPrimaryEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            return Ok(await _service.SetPrimaryEmergencyContactAsync(emergencyContactId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}/activate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> ActivateEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            return Ok(await _service.ActivateEmergencyContactAsync(emergencyContactId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> DeactivateEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            return Ok(await _service.DeactivateEmergencyContactAsync(emergencyContactId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // Address contacts
    [HttpGet("{employeeId:guid}/contacts")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeContactDto>>> GetContacts(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");

        try
        {
            return Ok(await _service.GetContactsAsync(employeeId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/contacts")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContactDto>> AddContact(Guid employeeId, [FromBody] CreateEmployeeContactDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddContactAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetContacts), new { employeeId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/contacts/{contactId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContactDto>> UpdateContact(Guid employeeId, Guid contactId, [FromBody] UpdateEmployeeContactDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contactId == Guid.Empty) return BadRequest("Invalid contact id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = contactId;

        try
        {
            var existing = await _service.GetContactByIdAsync(contactId, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.UpdateContactAsync(dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/contacts/{contactId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveContact(Guid employeeId, Guid contactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contactId == Guid.Empty) return BadRequest("Invalid contact id.");

        try
        {
            var contact = await _service.GetContactByIdAsync(contactId, cancellationToken);
            if (contact == null || contact.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveContactAsync(contactId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/contacts/{contactId:guid}/set-primary")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContactDto>> SetPrimaryContact(Guid employeeId, Guid contactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contactId == Guid.Empty) return BadRequest("Invalid contact id.");

        try
        {
            var contact = await _service.GetContactByIdAsync(contactId, cancellationToken);
            if (contact == null || contact.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.SetPrimaryContactAsync(contactId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all dependents for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of dependents for the employee.</returns>
    /// <response code="200">Dependents retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("{employeeId:guid}/dependents")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDependentReadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeDependentReadDto>>> GetDependents(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");

        try
        {
            return Ok(await _service.GetDependentsAsync(employeeId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/dependents")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDependentReadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDependentReadDto>> AddDependent(Guid employeeId, [FromBody] EmployeeDependentCreateDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddDependentAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetDependents), new { employeeId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/dependents/{dependentId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDependentReadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDependentReadDto>> UpdateDependent(Guid employeeId, Guid dependentId, [FromBody] EmployeeDependentUpdateDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (dependentId == Guid.Empty) return BadRequest("Invalid dependent id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = dependentId;

        try
        {
            var updated = await _service.UpdateDependentAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/dependents/{dependentId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDependent(Guid employeeId, Guid dependentId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (dependentId == Guid.Empty) return BadRequest("Invalid dependent id.");

        try
        {
            var ok = await _service.RemoveDependentAsync(employeeId, dependentId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Dependent benefits (under employee-dependent id)
    [HttpGet("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDependentBenefitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDependentBenefitDto>>> GetDependentBenefits(Guid employeeId, Guid employeeDependentId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");

        return Ok(await _service.GetDependentBenefitsAsync(employeeDependentId, cancellationToken));
    }

    [HttpPost("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDependentBenefitDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDependentBenefitDto>> AddDependentBenefit(Guid employeeId, Guid employeeDependentId, [FromBody] CreateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeDependentId = employeeDependentId;

        try
        {
            var created = await _service.AddDependentBenefitAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetDependentBenefits), new { employeeId, employeeDependentId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDependentBenefitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDependentBenefitDto>> UpdateDependentBenefit(Guid employeeId, Guid employeeDependentId, Guid dependentBenefitId, [FromBody] UpdateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (dependentBenefitId == Guid.Empty) return BadRequest("Invalid dependent benefit id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = dependentBenefitId;

        try
        {
            var updated = await _service.UpdateDependentBenefitAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDependentBenefit(Guid employeeId, Guid employeeDependentId, Guid dependentBenefitId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (dependentBenefitId == Guid.Empty) return BadRequest("Invalid dependent benefit id.");

        try
        {
            var ok = await _service.RemoveDependentBenefitAsync(dependentBenefitId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}/activate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDependentBenefitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDependentBenefitDto>> ActivateDependentBenefit(Guid employeeId, Guid employeeDependentId, Guid dependentBenefitId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (dependentBenefitId == Guid.Empty) return BadRequest("Invalid dependent benefit id.");

        try
        {
            return Ok(await _service.ActivateDependentBenefitAsync(dependentBenefitId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDependentBenefitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDependentBenefitDto>> DeactivateDependentBenefit(Guid employeeId, Guid employeeDependentId, Guid dependentBenefitId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (dependentBenefitId == Guid.Empty) return BadRequest("Invalid dependent benefit id.");

        try
        {
            return Ok(await _service.DeactivateDependentBenefitAsync(dependentBenefitId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Qualifications
    [HttpGet("{employeeId:guid}/qualifications")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeQualificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeQualificationDto>>> GetQualifications(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetQualificationsAsync(employeeId, cancellationToken));
    }

    [HttpPost("{employeeId:guid}/qualifications")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeQualificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeQualificationDto>> AddQualification(Guid employeeId, [FromBody] CreateEmployeeQualificationDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddQualificationAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetQualifications), new { employeeId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/qualifications/{qualificationId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeQualificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeQualificationDto>> UpdateQualification(Guid employeeId, Guid qualificationId, [FromBody] UpdateEmployeeQualificationDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (qualificationId == Guid.Empty) return BadRequest("Invalid qualification id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = qualificationId;

        try
        {
            var updated = await _service.UpdateQualificationAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/qualifications/{qualificationId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveQualification(Guid employeeId, Guid qualificationId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (qualificationId == Guid.Empty) return BadRequest("Invalid qualification id.");

        try
        {
            var ok = await _service.RemoveQualificationAsync(qualificationId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/qualifications/{qualificationId:guid}/verify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeQualificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeQualificationDto>> VerifyQualification(Guid employeeId, Guid qualificationId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (qualificationId == Guid.Empty) return BadRequest("Invalid qualification id.");

        try
        {
            return Ok(await _service.VerifyQualificationAsync(qualificationId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/qualifications/{qualificationId:guid}/unverify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeQualificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeQualificationDto>> UnverifyQualification(Guid employeeId, Guid qualificationId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (qualificationId == Guid.Empty) return BadRequest("Invalid qualification id.");

        try
        {
            return Ok(await _service.UnverifyQualificationAsync(qualificationId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// What the post asks of this person against what they hold (round 2, lane C3b). Leads the
    /// skills tab: held, held below the level asked for, or not held at all.
    /// </summary>
    [HttpGet("{employeeId:guid}/skill-requirements")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeSkillRequirementsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSkillRequirementsDto>> GetSkillRequirements(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        try { return Ok(await _service.GetSkillRequirementsAsync(employeeId, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // NEW ENDPOINTS: Skills & certifications
    [HttpGet("{employeeId:guid}/skills")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSkillDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeSkillDto>>> GetSkills(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetSkillsAsync(employeeId, cancellationToken));
    }

    [HttpPost("{employeeId:guid}/skills")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeSkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeSkillDto>> AddSkill(Guid employeeId, [FromBody] CreateEmployeeSkillDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddSkillAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetSkills), new { employeeId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/skills/{employeeSkillId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSkillDto>> UpdateSkill(Guid employeeId, Guid employeeSkillId, [FromBody] UpdateEmployeeSkillDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeSkillId == Guid.Empty) return BadRequest("Invalid employee skill id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = employeeSkillId;

        try
        {
            var updated = await _service.UpdateSkillAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/skills/{employeeSkillId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSkill(Guid employeeId, Guid employeeSkillId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeSkillId == Guid.Empty) return BadRequest("Invalid employee skill id.");

        try
        {
            var ok = await _service.RemoveSkillAsync(employeeSkillId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/skills/{employeeSkillId:guid}/verify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSkillDto>> VerifySkill(Guid employeeId, Guid employeeSkillId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeSkillId == Guid.Empty) return BadRequest("Invalid employee skill id.");

        try
        {
            return Ok(await _service.VerifySkillAsync(employeeSkillId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/skills/{employeeSkillId:guid}/unverify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSkillDto>> UnverifySkill(Guid employeeId, Guid employeeSkillId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeSkillId == Guid.Empty) return BadRequest("Invalid employee skill id.");

        try
        {
            return Ok(await _service.UnverifySkillAsync(employeeSkillId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Identification cards
    [HttpGet("{employeeId:guid}/identification-cards")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeIdentificationCardListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeIdentificationCardListDto>>> GetIdentificationCards(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetIdentificationCardsAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/identification-cards/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeIdentificationCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeIdentificationCardDetailDto?>> GetIdentificationCardById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");

        var item = await _service.GetIdentificationCardByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{employeeId:guid}/identification-cards")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeIdentificationCardDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeIdentificationCardDetailDto>> AddIdentificationCard(Guid employeeId, [FromBody] CreateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddIdentificationCardAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetIdentificationCardById), new { employeeId, id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/identification-cards/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeIdentificationCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeIdentificationCardDetailDto>> UpdateIdentificationCard(Guid employeeId, Guid id, [FromBody] UpdateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = id;

        try
        {
            var updated = await _service.UpdateIdentificationCardAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/identification-cards/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveIdentificationCard(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");

        try
        {
            var ok = await _service.RemoveIdentificationCardAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/identification-cards/{id:guid}/verify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeIdentificationCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeIdentificationCardDetailDto>> VerifyIdentificationCard(Guid employeeId, Guid id, [FromBody] VerifyIdentificationCardRequest request, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");
        if (request == null || request.VerifiedDate == default) return BadRequest("VerifiedDate is required.");

        try
        {
            var updated = await _service.VerifyIdentificationCardAsync(id, request.VerifiedDate, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/identification-cards/{id:guid}/unverify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeIdentificationCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeIdentificationCardDetailDto>> UnverifyIdentificationCard(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");

        try
        {
            var updated = await _service.UnverifyIdentificationCardAsync(id, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Work history
    [HttpGet("{employeeId:guid}/work-histories")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeWorkHistoryListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeWorkHistoryListDto>>> GetWorkHistories(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetWorkHistoriesAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/work-histories/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeWorkHistoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeWorkHistoryDetailDto?>> GetWorkHistoryById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid work history id.");

        var item = await _service.GetWorkHistoryByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{employeeId:guid}/work-histories")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeWorkHistoryDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeWorkHistoryDetailDto>> AddWorkHistory(Guid employeeId, [FromBody] CreateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddWorkHistoryAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetWorkHistoryById), new { employeeId, id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/work-histories/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeWorkHistoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeWorkHistoryDetailDto>> UpdateWorkHistory(Guid employeeId, Guid id, [FromBody] UpdateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid work history id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = id;

        try
        {
            var updated = await _service.UpdateWorkHistoryAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/work-histories/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveWorkHistory(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid work history id.");

        try
        {
            var ok = await _service.RemoveWorkHistoryAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Contracts
    [HttpGet("{employeeId:guid}/contracts")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeContractDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeContractDetailDto>>> GetContracts(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetContractsAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/contracts/active")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto?>> GetActiveContract(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");

        var contract = await _service.GetActiveContractAsync(employeeId, cancellationToken);
        return contract == null ? NotFound() : Ok(contract);
    }

    [HttpPost("{employeeId:guid}/contracts")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeContractDetailDto>> AddContract(Guid employeeId, [FromBody] CreateEmployeeContractDetailDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddContractAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetContracts), new { employeeId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/contracts/{contractId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto>> UpdateContract(Guid employeeId, Guid contractId, [FromBody] UpdateEmployeeContractDetailDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = contractId;

        try
        {
            var updated = await _service.UpdateContractAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/contracts/{contractId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveContract(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");

        try
        {
            var ok = await _service.RemoveContractAsync(contractId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/contracts/{contractId:guid}/activate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto>> ActivateContract(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");

        try
        {
            return Ok(await _service.ActivateContractAsync(contractId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/contracts/{contractId:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto>> DeactivateContract(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");

        try
        {
            return Ok(await _service.DeactivateContractAsync(contractId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/contracts/{contractId:guid}/terminate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto>> TerminateContract(Guid employeeId, Guid contractId, [FromBody] TerminateContractRequest request, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");
        if (request == null || request.TerminationDate == default || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("TerminationDate and Reason are required.");

        try
        {
            var updated = await _service.TerminateContractAsync(contractId, request.TerminationDate, request.Reason, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Expatriate assignments
    [HttpGet("{employeeId:guid}/expatriate-assignments")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<ExpatriateAssignmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpatriateAssignmentListDto>>> GetExpatriateAssignments(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetExpatriateAssignmentsAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/expatriate-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(ExpatriateAssignmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpatriateAssignmentDetailDto?>> GetExpatriateAssignmentById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid expatriate assignment id.");

        var item = await _service.GetExpatriateAssignmentByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{employeeId:guid}/expatriate-assignments")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(ExpatriateAssignmentDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpatriateAssignmentDetailDto>> AddExpatriateAssignment(Guid employeeId, [FromBody] CreateExpatriateAssignmentDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddExpatriateAssignmentAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetExpatriateAssignmentById), new { employeeId, id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/expatriate-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(ExpatriateAssignmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpatriateAssignmentDetailDto>> UpdateExpatriateAssignment(Guid employeeId, Guid id, [FromBody] UpdateExpatriateAssignmentDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid expatriate assignment id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = id;

        try
        {
            var updated = await _service.UpdateExpatriateAssignmentAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/expatriate-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveExpatriateAssignment(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid expatriate assignment id.");

        try
        {
            var ok = await _service.RemoveExpatriateAssignmentAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Expatriate family members ────────────────────────────────────────────
    //
    // ⚠ `FamilyAccompanying` was a bare bool. The record could say a family had come and never who
    // they were, so nobody could count the residence permits owed or see whose lapsed next.

    [HttpGet("{employeeId:guid}/expatriate-assignments/{assignmentId:guid}/family")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<ExpatriateFamilyMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpatriateFamilyMemberDto>>> GetExpatriateFamily(
        Guid employeeId, Guid assignmentId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (assignmentId == Guid.Empty) return BadRequest("Invalid expatriate assignment id.");
        return Ok(await _service.GetExpatriateFamilyMembersAsync(assignmentId, cancellationToken));
    }

    [HttpPost("{employeeId:guid}/expatriate-assignments/{assignmentId:guid}/family")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(ExpatriateFamilyMemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ExpatriateFamilyMemberDto>> AddExpatriateFamilyMember(
        Guid employeeId, Guid assignmentId,
        [FromBody] CreateExpatriateFamilyMemberDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (assignmentId == Guid.Empty) return BadRequest("Invalid expatriate assignment id.");
        // The route wins over the body, so a mismatched id cannot file a person against another
        // assignment — the same rule every other nested write in this controller applies.
        dto.ExpatriateAssignmentId = assignmentId;

        try
        {
            var created = await _service.AddExpatriateFamilyMemberAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetExpatriateFamily),
                new { employeeId, assignmentId }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/expatriate-assignments/{assignmentId:guid}/family/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(ExpatriateFamilyMemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExpatriateFamilyMemberDto>> UpdateExpatriateFamilyMember(
        Guid employeeId, Guid assignmentId, Guid id,
        [FromBody] UpdateExpatriateFamilyMemberDto dto, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid family member id.");
        dto.Id = id;

        try
        {
            return Ok(await _service.UpdateExpatriateFamilyMemberAsync(dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/expatriate-assignments/{assignmentId:guid}/family/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveExpatriateFamilyMember(
        Guid employeeId, Guid assignmentId, Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid family member id.");

        try
        {
            var ok = await _service.RemoveExpatriateFamilyMemberAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Position history
    [HttpGet("{employeeId:guid}/position-histories")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionHistoryListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionHistoryListDto>>> GetPositionHistories(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetPositionHistoryAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/position-histories/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeePositionHistoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionHistoryDetailDto?>> GetPositionHistoryById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid position history id.");

        var item = await _service.GetPositionHistoryByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{employeeId:guid}/position-histories")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeePositionHistoryDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePositionHistoryDetailDto>> AddPositionHistory(Guid employeeId, [FromBody] CreateEmployeePositionHistoryDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddPositionHistoryAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetPositionHistoryById), new { employeeId, id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/position-histories/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeePositionHistoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionHistoryDetailDto>> UpdatePositionHistory(Guid employeeId, Guid id, [FromBody] UpdateEmployeePositionHistoryDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid position history id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = id;

        try
        {
            var updated = await _service.UpdatePositionHistoryAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/position-histories/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePositionHistory(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid position history id.");

        try
        {
            var ok = await _service.RemovePositionHistoryAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Salary assignments
    [HttpGet("{employeeId:guid}/salary-assignments")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSalaryAssignmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeSalaryAssignmentListDto>>> GetSalaryAssignments(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetSalaryAssignmentsAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/salary-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeSalaryAssignmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSalaryAssignmentDetailDto?>> GetSalaryAssignmentById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid salary assignment id.");

        var item = await _service.GetSalaryAssignmentByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{employeeId:guid}/salary-assignments")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeSalaryAssignmentDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeSalaryAssignmentDetailDto>> AssignSalary(Guid employeeId, [FromBody] CreateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AssignSalaryAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetSalaryAssignmentById), new { employeeId, id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/salary-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeSalaryAssignmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSalaryAssignmentDetailDto>> UpdateSalaryAssignment(Guid employeeId, Guid id, [FromBody] UpdateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid salary assignment id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = id;

        try
        {
            var updated = await _service.UpdateSalaryAssignmentAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/salary-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSalaryAssignment(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid salary assignment id.");

        try
        {
            var ok = await _service.RemoveSalaryAssignmentAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // ── Payroll membership ────────────────────────────────────────────────────────────────
    // HR's "on payroll" statement beside payroll's own profile. The flag itself is written through
    // the ordinary create/update; these are the reads that show whether payroll agrees.

    /// <summary>Both sides for one employee: HR's flag and pay basis, payroll's profile, and the discrepancy if any.</summary>
    [HttpGet("{employeeId:guid}/payroll-status")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeePayrollStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePayrollStatusDto>> GetPayrollStatus(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        try
        {
            return Ok(await _payrollMembership.GetStatusAsync(employeeId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Payroll's employee profile for this person — the window the Salary tab hosts — read through
    /// payroll's own service. 404 when payroll has no profile for them.
    /// </summary>
    /// <remarks>
    /// Round-2 lane E1 (§ 6.5.1). Payroll exposes no by-employee read (§ 7.1, asked), so this is
    /// HR's door: filter payroll's search to the exact employee. Gated on compensation, not on the
    /// employee record — it shows what the person is paid and how it is split.
    /// </remarks>
    [HttpGet("{employeeId:guid}/payroll-profile")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(PayrollEmployeeProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollEmployeeProfileDto>> GetPayrollProfile(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        try
        {
            var profile = await _payrollMembership.GetPayrollProfileAsync(employeeId, cancellationToken);
            return profile == null
                ? NotFound(new { message = "Payroll has no profile for this employee yet." })
                : Ok(profile);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>Scale or negotiated, and why. Negotiated closes any open grade placement.</summary>
    [HttpPut("{employeeId:guid}/pay-basis")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto>> SetPayBasis(Guid employeeId, [FromBody] SetEmployeePayBasisDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            return Ok(await _service.SetPayBasisAsync(employeeId, dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Every live employee where HR's statement and payroll's profile disagree, or where an
    /// on-payroll statement has no pay basis. Compensation-level, not employee-level: it lists pay.
    /// </summary>
    [HttpGet("payroll-reconciliation")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(PayrollReconciliationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayrollReconciliationDto>> GetPayrollReconciliation(CancellationToken cancellationToken)
        => Ok(await _payrollMembership.GetReconciliationAsync(cancellationToken));

    // NEW ENDPOINTS: Referees
    [HttpGet("{employeeId:guid}/referees")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeRefereeListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeRefereeListDto>>> GetReferees(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetRefereesAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/referees/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto?>> GetRefereeById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        var item = await _service.GetRefereeByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{employeeId:guid}/referees")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> AddReferee(Guid employeeId, [FromBody] CreateEmployeeRefereeDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddRefereeAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetRefereeById), new { employeeId, id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/referees/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> UpdateReferee(Guid employeeId, Guid id, [FromBody] UpdateEmployeeRefereeDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = id;

        try
        {
            var updated = await _service.UpdateRefereeAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/referees/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            var ok = await _service.RemoveRefereeAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/referees/{id:guid}/set-primary")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> SetPrimaryReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            return Ok(await _service.SetPrimaryRefereeAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/referees/{id:guid}/activate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> ActivateReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            return Ok(await _service.ActivateRefereeAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/referees/{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> DeactivateReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            return Ok(await _service.DeactivateRefereeAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // NEW ENDPOINTS: Guarantors
    [HttpGet("{employeeId:guid}/guarantors")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeGuarantorListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeGuarantorListDto>>> GetGuarantors(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetGuarantorsAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/guarantors/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto?>> GetGuarantorById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        var item = await _service.GetGuarantorByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{employeeId:guid}/guarantors")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> AddGuarantor(Guid employeeId, [FromBody] CreateEmployeeGuarantorDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.EmployeeId = employeeId;

        try
        {
            var created = await _service.AddGuarantorAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetGuarantorById), new { employeeId, id = created.Id }, created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/guarantors/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> UpdateGuarantor(Guid employeeId, Guid id, [FromBody] UpdateEmployeeGuarantorDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.Id = id;

        try
        {
            var updated = await _service.UpdateGuarantorAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/guarantors/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            var ok = await _service.RemoveGuarantorAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/set-primary")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> SetPrimaryGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            return Ok(await _service.SetPrimaryGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/verify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> VerifyGuarantor(Guid employeeId, Guid id, [FromBody] VerifyGuarantorRequest request, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");
        if (request == null || request.VerifiedByEmployeeId == Guid.Empty || request.VerifiedDate == default)
            return BadRequest("VerifiedByEmployeeId and VerifiedDate are required.");

        try
        {
            return Ok(await _service.VerifyGuarantorAsync(id, request.VerifiedByEmployeeId, request.VerifiedDate, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/unverify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> UnverifyGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            return Ok(await _service.UnverifyGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/activate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> ActivateGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            return Ok(await _service.ActivateGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> DeactivateGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            return Ok(await _service.DeactivateGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpGet("{employeeId:guid}/bank-details")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeBankDetailDto>>> GetBankDetails(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetBankDetailsAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/bank-details/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> GetBankDetailById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        var result = await _service.GetBankDetailByIdAsync(id, cancellationToken);
        if (result == null || result.EmployeeId != employeeId) return NotFound();
        return Ok(result);
    }

    [HttpPost("{employeeId:guid}/bank-details")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> AddBankDetail(Guid employeeId, [FromBody] CreateEmployeeBankDetailDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        dto.EmployeeId = employeeId;
        try
        {
            return Ok(await _service.AddBankDetailAsync(dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPut("{employeeId:guid}/bank-details/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> UpdateBankDetail(Guid employeeId, Guid id, [FromBody] UpdateEmployeeBankDetailDto dto, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        dto.Id = id;
        try
        {
            var existing = await _service.GetBankDetailByIdAsync(id, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.UpdateBankDetailAsync(dto, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpDelete("{employeeId:guid}/bank-details/{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    public async Task<ActionResult> RemoveBankDetail(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        try
        {
            var existing = await _service.GetBankDetailByIdAsync(id, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            var removed = await _service.RemoveBankDetailAsync(id, cancellationToken);
            return removed ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/bank-details/{id:guid}/set-primary")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> SetPrimaryBankDetail(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        try
        {
            var existing = await _service.GetBankDetailByIdAsync(id, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.SetPrimaryBankDetailAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/bank-details/{id:guid}/verify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> VerifyBankDetail(Guid employeeId, Guid id, [FromQuery] Guid verifiedById, [FromQuery] DateTime verifiedDate, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        if (verifiedById == Guid.Empty) return BadRequest("verifiedById is required.");
        try
        {
            var existing = await _service.GetBankDetailByIdAsync(id, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.VerifyBankDetailAsync(id, verifiedById, verifiedDate, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/bank-details/{id:guid}/unverify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> UnverifyBankDetail(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        try
        {
            var existing = await _service.GetBankDetailByIdAsync(id, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.UnverifyBankDetailAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/bank-details/{id:guid}/activate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> ActivateBankDetail(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        try
        {
            var existing = await _service.GetBankDetailByIdAsync(id, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.ActivateBankDetailAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpPost("{employeeId:guid}/bank-details/{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<ActionResult<EmployeeBankDetailDto>> DeactivateBankDetail(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        try
        {
            var existing = await _service.GetBankDetailByIdAsync(id, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.DeactivateBankDetailAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    #endregion

    #region Private helpers

    private ActionResult ToClientError(Exception ex)
    {
        // Align to controller rules: 404 for not-found; 400 for other invalid requests.
        // We rely on service exception messages (kept stable) to avoid extra round-trips.
        if (ex is ArgumentException && IsNotFound(ex.Message))
            return NotFound(new { message = ex.Message });

        if (ex is InvalidOperationException || ex is ArgumentException)
            return BadRequest(new { message = ex.Message });

        _logger.LogError(ex, "Unexpected error in EmployeesController");
        return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
    }

    private static bool IsNotFound(string? message)
        => !string.IsNullOrWhiteSpace(message) &&
           message.Contains("not found", StringComparison.OrdinalIgnoreCase);

    public sealed class ReinstateEmployeeRequest
    {
        public string? Notes { get; set; }
    }

    public sealed class VerifyIdentificationCardRequest
    {
        public DateTime VerifiedDate { get; set; }
    }

    public sealed class TerminateContractRequest
    {
        public DateOnly TerminationDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class VerifyGuarantorRequest
    {
        public Guid VerifiedByEmployeeId { get; set; }
        public DateTime VerifiedDate { get; set; }
    }

    #endregion
}
