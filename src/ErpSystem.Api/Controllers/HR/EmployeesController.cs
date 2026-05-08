using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/[controller]")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _service;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(
        IEmployeeService service,
        ILogger<EmployeesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    #region CRUD

    /// <summary>
    /// Retrieves a summary of an employee by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The employee summary information.</returns>
    /// <response code="200">Employee found and returned successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto?>> GetEmployee(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        var result = await _service.GetEmployeeSummaryByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information about an employee by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed employee information.</returns>
    /// <response code="200">Employee details found and returned successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("{id:guid}/details")]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto?>> GetEmployeeDetails(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        var result = await _service.GetEmployeeDetailsByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves the complete profile of an employee including all related information.
    /// </summary>
    /// <param name="id">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete employee profile with all related data.</returns>
    /// <response code="200">Employee profile found and returned successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("{id:guid}/profile")]
    [ProducesResponseType(typeof(EmployeeFullProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeFullProfileDto?>> GetEmployeeFullProfile(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest("Invalid employee id.");

        var result = await _service.GetEmployeeFullProfileByIdAsync(id, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves a summary of an employee by their employee number.
    /// </summary>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The employee summary information.</returns>
    /// <response code="200">Employee found and returned successfully.</response>
    /// <response code="400">Employee number is required.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("number/{employeeNumber}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto?>> GetByEmployeeNumber(string employeeNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return BadRequest("Employee number is required.");

        var result = await _service.GetEmployeeSummaryByEmployeeNumberAsync(employeeNumber, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information about an employee by their employee number.
    /// </summary>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed employee information.</returns>
    /// <response code="200">Employee details found and returned successfully.</response>
    /// <response code="400">Employee number is required.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("number/{employeeNumber}/details")]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto?>> GetEmployeeDetailsByEmployeeNumber(string employeeNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return BadRequest("Employee number is required.");

        var result = await _service.GetEmployeeDetailsByEmployeeNumberAsync(employeeNumber, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves the complete profile of an employee by their employee number.
    /// </summary>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete employee profile with all related data.</returns>
    /// <response code="200">Employee profile found and returned successfully.</response>
    /// <response code="400">Employee number is required.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("number/{employeeNumber}/profile")]
    [ProducesResponseType(typeof(EmployeeFullProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeFullProfileDto?>> GetEmployeeFullProfileByEmployeeNumber(string employeeNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return BadRequest("Employee number is required.");

        var result = await _service.GetEmployeeFullProfileByEmployeeNumberAsync(employeeNumber, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves a summary of an employee by their email address.
    /// </summary>
    /// <param name="email">The employee's email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The employee summary information.</returns>
    /// <response code="200">Employee found and returned successfully.</response>
    /// <response code="400">Email is required.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("email/{email}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto?>> GetByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email is required.");

        var result = await _service.GetEmployeeSummaryByEmailAsync(email, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Creates a new employee.
    /// </summary>
    /// <param name="dto">The employee creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created employee with detailed information.</returns>
    /// <response code="201">Employee created successfully.</response>
    /// <response code="400">Invalid employee data provided.</response>
    [HttpPost]
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
    /// Updates an existing employee's information.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to update.</param>
    /// <param name="dto">The updated employee data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated employee with detailed information.</returns>
    /// <response code="200">Employee updated successfully.</response>
    /// <response code="400">Invalid employee identifier or data provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPut("{id:guid}")]
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

    /// <summary>
    /// Deletes an employee from the system.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Employee deleted successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpDelete("{id:guid}")]
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

    /// <summary>
    /// Deactivates an employee, making them inactive but preserving their data.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to deactivate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A success message.</returns>
    /// <response code="200">Employee deactivated successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPost("{id:guid}/deactivate")]
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

    /// <summary>
    /// Activates a previously deactivated employee, restoring them to active status.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A success message.</returns>
    /// <response code="200">Employee activated successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPost("{id:guid}/activate")]
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

    /// <summary>
    /// Terminates an employee's employment with the organization.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to terminate.</param>
    /// <param name="dto">The termination details including date and reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A success message.</returns>
    /// <response code="200">Employee terminated successfully.</response>
    /// <response code="400">Invalid employee identifier or termination data provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPost("{id:guid}/terminate")]
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

    /// <summary>
    /// Reinstates a previously terminated employee, returning them to active status.
    /// </summary>
    /// <param name="id">The unique identifier of the employee to reinstate.</param>
    /// <param name="request">Optional reinstatement details and notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A success message.</returns>
    /// <response code="200">Employee reinstated successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPost("{id:guid}/reinstate")]
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

    /// <summary>
    /// Retrieves a paginated list of employees with optional search criteria.
    /// </summary>
    /// <param name="search">The search criteria and filters.</param>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of employee summaries.</returns>
    /// <response code="200">Employees retrieved successfully.</response>
    /// <response code="400">Invalid search criteria provided.</response>
    [HttpPost("paged")]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetPaged([FromBody] EmployeeSearchDto search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _service.GetEmployeesPagedAsync(search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all employees belonging to a specific organization unit.
    /// </summary>
    /// <param name="organizationUnitId">The unique identifier of the organization unit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employees in the organization unit.</returns>
    /// <response code="200">Employees retrieved successfully.</response>
    /// <response code="400">Invalid organization unit identifier provided.</response>
    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetByOrganizationUnit(Guid organizationUnitId, CancellationToken cancellationToken)
    {
        if (organizationUnitId == Guid.Empty) return BadRequest("Invalid organization unit id.");
        return Ok(await _service.GetEmployeesByOrganizationUnitAsync(organizationUnitId, cancellationToken));
    }

    /// <summary>
    /// Retrieves all employees at a specific organization level.
    /// </summary>
    /// <param name="organizationLevelId">The unique identifier of the organization level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employees at the organization level.</returns>
    /// <response code="200">Employees retrieved successfully.</response>
    /// <response code="400">Invalid organization level identifier provided.</response>
    [HttpGet("organization-level/{organizationLevelId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetByOrganizationLevel(Guid organizationLevelId, CancellationToken cancellationToken)
    {
        if (organizationLevelId == Guid.Empty) return BadRequest("Invalid organization level id.");
        return Ok(await _service.GetEmployeesByOrganizationLevelAsync(organizationLevelId, cancellationToken));
    }

    /// <summary>
    /// Retrieves all employees assigned to a specific location.
    /// </summary>
    /// <param name="locationId">The unique identifier of the location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employees at the location.</returns>
    /// <response code="200">Employees retrieved successfully.</response>
    /// <response code="400">Invalid location identifier provided.</response>
    [HttpGet("location/{locationId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetByLocation(Guid locationId, CancellationToken cancellationToken)
    {
        if (locationId == Guid.Empty) return BadRequest("Invalid location id.");
        return Ok(await _service.GetEmployeesByLocationAsync(locationId, cancellationToken));
    }

    /// <summary>
    /// Retrieves all direct reports (subordinates) of a specific manager.
    /// </summary>
    /// <param name="managerId">The unique identifier of the manager.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employees directly reporting to the manager.</returns>
    /// <response code="200">Direct reports retrieved successfully.</response>
    /// <response code="400">Invalid manager identifier provided.</response>
    [HttpGet("manager/{managerId:guid}/direct-reports")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetDirectReports(Guid managerId, CancellationToken cancellationToken)
    {
        if (managerId == Guid.Empty) return BadRequest("Invalid manager id.");
        return Ok(await _service.GetDirectReportsAsync(managerId, cancellationToken));
    }

    /// <summary>
    /// Retrieves the complete management chain for an employee, from immediate supervisor to top-level management.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of managers in hierarchical order.</returns>
    /// <response code="200">Management chain retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
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

    /// <summary>
    /// Retrieves all maintenance technicians.
    /// </summary>
    /// <returns>A list of maintenance technicians.</returns>
    /// <response code="200">Technicians retrieved successfully.</response>
    [HttpGet("technicians")]
    public async Task<ActionResult<IEnumerable<MaintenanceTechnicianDto>>> GetTechnicians()
        => Ok(await _service.GetMaintenanceTechniciansAsync());

    /// <summary>
    /// Retrieves all available maintenance technicians who are not currently assigned to tasks.
    /// </summary>
    /// <returns>A list of available maintenance technicians.</returns>
    /// <response code="200">Available technicians retrieved successfully.</response>
    [HttpGet("technicians/available")]
    public async Task<ActionResult<IEnumerable<MaintenanceTechnicianDto>>> GetAvailableTechnicians()
        => Ok(await _service.GetAvailableTechniciansAsync());

    /// <summary>
    /// Retrieves a specific maintenance technician by their employee identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <returns>The maintenance technician details.</returns>
    /// <response code="200">Technician found and returned successfully.</response>
    /// <response code="404">Technician not found.</response>
    [HttpGet("technicians/{employeeId:guid}")]
    public async Task<ActionResult<MaintenanceTechnicianDto?>> GetTechnician(Guid employeeId)
    {
        var result = await _service.GetTechnicianByIdAsync(employeeId);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves the availability status of a specific maintenance technician.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <returns>The technician's availability information.</returns>
    /// <response code="200">Availability information retrieved successfully.</response>
    /// <response code="404">Technician not found.</response>
    [HttpGet("technicians/{employeeId:guid}/availability")]
    public async Task<ActionResult<TechnicianAvailabilityDto?>> GetTechnicianAvailability(Guid employeeId)
    {
        var result = await _service.GetTechnicianAvailabilityAsync(employeeId);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Retrieves all maintenance technicians who have a specific skill.
    /// </summary>
    /// <param name="skillId">The unique identifier of the skill.</param>
    /// <param name="minLevel">Optional minimum skill level filter.</param>
    /// <returns>A list of technicians with the specified skill.</returns>
    /// <response code="200">Technicians retrieved successfully.</response>
    [HttpGet("technicians/skill/{skillId:guid}")]
    public async Task<ActionResult<IEnumerable<MaintenanceTechnicianDto>>> GetTechniciansWithSkill(
        Guid skillId,
        [FromQuery] SkillLevel? minLevel = null)
        => Ok(await _service.GetTechniciansWithSkillAsync(skillId, minLevel));

    #endregion

    #region Stats

    /// <summary>
    /// Retrieves the total count of all employees in the system.
    /// </summary>
    /// <returns>The total number of employees.</returns>
    /// <response code="200">Count retrieved successfully.</response>
    [HttpGet("stats/total")]
    public async Task<ActionResult<int>> GetTotalEmployeeCount()
        => Ok(await _service.GetTotalEmployeeCountAsync());

    /// <summary>
    /// Retrieves the count of active employees in the system.
    /// </summary>
    /// <returns>The number of active employees.</returns>
    /// <response code="200">Count retrieved successfully.</response>
    [HttpGet("stats/active")]
    public async Task<ActionResult<int>> GetActiveEmployeeCount()
        => Ok(await _service.GetActiveEmployeeCountAsync());

    /// <summary>
    /// Retrieves employee counts grouped by their status.
    /// </summary>
    /// <returns>A dictionary with status as key and count as value.</returns>
    /// <response code="200">Statistics retrieved successfully.</response>
    [HttpGet("stats/by-status")]
    public async Task<ActionResult<Dictionary<StaffStatus, int>>> GetEmployeeCountByStatus()
        => Ok(await _service.GetEmployeeCountByStatusAsync());

    /// <summary>
    /// Retrieves employee counts grouped by their department.
    /// </summary>
    /// <returns>A dictionary with department name as key and count as value.</returns>
    /// <response code="200">Statistics retrieved successfully.</response>
    [HttpGet("stats/by-department")]
    public async Task<ActionResult<Dictionary<string, int>>> GetEmployeeCountByDepartment()
        => Ok(await _service.GetEmployeeCountByDepartmentAsync());

    #endregion

    #region Relationship subresources (Employee aggregate)

    /// <summary>
    /// Retrieves all emergency contacts for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of emergency contacts for the employee.</returns>
    /// <response code="200">Emergency contacts retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("{employeeId:guid}/emergency-contacts")]
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

    /// <summary>
    /// Adds a new emergency contact for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The emergency contact data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created emergency contact.</returns>
    /// <response code="201">Emergency contact created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPost("{employeeId:guid}/emergency-contacts")]
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

    /// <summary>
    /// Updates an existing emergency contact for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="emergencyContactId">The unique identifier of the emergency contact.</param>
    /// <param name="dto">The updated emergency contact data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated emergency contact.</returns>
    /// <response code="200">Emergency contact updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Emergency contact not found.</response>
    [HttpPut("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}")]
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
            var existing = await _service.GetEmergencyContactByIdAsync(emergencyContactId, cancellationToken);
            if (existing == null || existing.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateEmergencyContactAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes an emergency contact from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="emergencyContactId">The unique identifier of the emergency contact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Emergency contact removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Emergency contact not found.</response>
    [HttpDelete("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            var contact = await _service.GetEmergencyContactByIdAsync(emergencyContactId, cancellationToken);
            if (contact == null || contact.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveEmergencyContactAsync(emergencyContactId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Sets an emergency contact as the primary contact for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="emergencyContactId">The unique identifier of the emergency contact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated emergency contact.</returns>
    /// <response code="200">Primary contact set successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Emergency contact not found.</response>
    [HttpPost("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}/set-primary")]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> SetPrimaryEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            var contact = await _service.GetEmergencyContactByIdAsync(emergencyContactId, cancellationToken);
            if (contact == null || contact.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.SetPrimaryEmergencyContactAsync(emergencyContactId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Activates an emergency contact, making it available for use.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="emergencyContactId">The unique identifier of the emergency contact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated emergency contact.</returns>
    /// <response code="200">Emergency contact activated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Emergency contact not found.</response>
    [HttpPost("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}/activate")]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> ActivateEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            var contact = await _service.GetEmergencyContactByIdAsync(emergencyContactId, cancellationToken);
            if (contact == null || contact.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.ActivateEmergencyContactAsync(emergencyContactId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Deactivates an emergency contact, making it temporarily unavailable.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="emergencyContactId">The unique identifier of the emergency contact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deactivated emergency contact.</returns>
    /// <response code="200">Emergency contact deactivated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Emergency contact not found.</response>
    [HttpPost("{employeeId:guid}/emergency-contacts/{emergencyContactId:guid}/deactivate")]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> DeactivateEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (emergencyContactId == Guid.Empty) return BadRequest("Invalid emergency contact id.");

        try
        {
            var contact = await _service.GetEmergencyContactByIdAsync(emergencyContactId, cancellationToken);
            if (contact == null || contact.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.DeactivateEmergencyContactAsync(emergencyContactId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    // Address contacts
    [HttpGet("{employeeId:guid}/contacts")]
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

    /// <summary>
    /// Adds a new dependent for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The dependent data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created dependent.</returns>
    /// <response code="201">Dependent created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPost("{employeeId:guid}/dependents")]
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

    /// <summary>
    /// Updates an existing dependent for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dependentId">The unique identifier of the dependent.</param>
    /// <param name="dto">The updated dependent data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated dependent.</returns>
    /// <response code="200">Dependent updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Dependent not found.</response>
    [HttpPut("{employeeId:guid}/dependents/{dependentId:guid}")]
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
            var dependent = await _service.GetDependentAsync(dependentId, cancellationToken);
            if (dependent == null || dependent.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateDependentAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a dependent from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dependentId">The unique identifier of the dependent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Dependent removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Dependent not found.</response>
    [HttpDelete("{employeeId:guid}/dependents/{dependentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDependent(Guid employeeId, Guid dependentId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (dependentId == Guid.Empty) return BadRequest("Invalid dependent id.");

        try
        {
            // Ownership check: ensure dependent belongs to the specified employee
            var dependent = await _service.GetDependentAsync(dependentId, cancellationToken);
            if (dependent == null || dependent.EmployeeId != employeeId)
                return NotFound();

            var ok = await _service.RemoveDependentAsync(dependentId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all benefits assigned to a specific dependent.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeDependentId">The unique identifier of the employee dependent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of dependent benefits.</returns>
    /// <response code="200">Dependent benefits retrieved successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    [HttpGet("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDependentBenefitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeDependentBenefitDto>>> GetDependentBenefits(Guid employeeId, Guid employeeDependentId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");

        return Ok(await _service.GetDependentBenefitsAsync(employeeDependentId, cancellationToken));
    }

    /// <summary>
    /// Adds a new benefit for a dependent.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeDependentId">The unique identifier of the employee dependent.</param>
    /// <param name="dto">The dependent benefit data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created dependent benefit.</returns>
    /// <response code="201">Dependent benefit created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits")]
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

    /// <summary>
    /// Updates an existing benefit for a dependent.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeDependentId">The unique identifier of the employee dependent.</param>
    /// <param name="dependentBenefitId">The unique identifier of the dependent benefit.</param>
    /// <param name="dto">The updated dependent benefit data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated dependent benefit.</returns>
    /// <response code="200">Dependent benefit updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Dependent benefit not found.</response>
    [HttpPut("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}")]
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
            var benefit = await _service.GetDependentBenefitByIdAsync(dependentBenefitId, cancellationToken);
            if (benefit == null || benefit.EmployeeDependentId != employeeDependentId) return NotFound();
            var dep = await _service.GetDependentAsync(employeeDependentId, cancellationToken);
            if (dep == null || dep.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateDependentBenefitAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a benefit from a dependent's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeDependentId">The unique identifier of the employee dependent.</param>
    /// <param name="dependentBenefitId">The unique identifier of the dependent benefit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Dependent benefit removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Dependent benefit not found.</response>
    [HttpDelete("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDependentBenefit(Guid employeeId, Guid employeeDependentId, Guid dependentBenefitId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (dependentBenefitId == Guid.Empty) return BadRequest("Invalid dependent benefit id.");

        try
        {
            var benefit = await _service.GetDependentBenefitByIdAsync(dependentBenefitId, cancellationToken);
            if (benefit == null || benefit.EmployeeDependentId != employeeDependentId) return NotFound();
            var dep = await _service.GetDependentAsync(employeeDependentId, cancellationToken);
            if (dep == null || dep.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveDependentBenefitAsync(dependentBenefitId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Activates a dependent benefit, making it active and available.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeDependentId">The unique identifier of the employee dependent.</param>
    /// <param name="dependentBenefitId">The unique identifier of the dependent benefit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated dependent benefit.</returns>
    /// <response code="200">Dependent benefit activated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Dependent benefit not found.</response>
    [HttpPost("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}/activate")]
    [ProducesResponseType(typeof(EmployeeDependentBenefitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDependentBenefitDto>> ActivateDependentBenefit(Guid employeeId, Guid employeeDependentId, Guid dependentBenefitId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (dependentBenefitId == Guid.Empty) return BadRequest("Invalid dependent benefit id.");

        try
        {
            var benefit = await _service.GetDependentBenefitByIdAsync(dependentBenefitId, cancellationToken);
            if (benefit == null || benefit.EmployeeDependentId != employeeDependentId) return NotFound();
            var dep = await _service.GetDependentAsync(employeeDependentId, cancellationToken);
            if (dep == null || dep.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.ActivateDependentBenefitAsync(dependentBenefitId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Deactivates a dependent benefit, making it temporarily unavailable.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeDependentId">The unique identifier of the employee dependent.</param>
    /// <param name="dependentBenefitId">The unique identifier of the dependent benefit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deactivated dependent benefit.</returns>
    /// <response code="200">Dependent benefit deactivated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Dependent benefit not found.</response>
    [HttpPost("{employeeId:guid}/dependents/{employeeDependentId:guid}/benefits/{dependentBenefitId:guid}/deactivate")]
    [ProducesResponseType(typeof(EmployeeDependentBenefitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDependentBenefitDto>> DeactivateDependentBenefit(Guid employeeId, Guid employeeDependentId, Guid dependentBenefitId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeDependentId == Guid.Empty) return BadRequest("Invalid employee dependent id.");
        if (dependentBenefitId == Guid.Empty) return BadRequest("Invalid dependent benefit id.");

        try
        {
            var benefit = await _service.GetDependentBenefitByIdAsync(dependentBenefitId, cancellationToken);
            if (benefit == null || benefit.EmployeeDependentId != employeeDependentId) return NotFound();
            var dep = await _service.GetDependentAsync(employeeDependentId, cancellationToken);
            if (dep == null || dep.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.DeactivateDependentBenefitAsync(dependentBenefitId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all qualifications for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employee qualifications.</returns>
    /// <response code="200">Qualifications retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/qualifications")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeQualificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeQualificationDto>>> GetQualifications(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetQualificationsAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Adds a new qualification for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The qualification data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created qualification.</returns>
    /// <response code="201">Qualification created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/qualifications")]
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

    /// <summary>
    /// Updates an existing qualification for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="qualificationId">The unique identifier of the qualification.</param>
    /// <param name="dto">The updated qualification data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated qualification.</returns>
    /// <response code="200">Qualification updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Qualification not found.</response>
    [HttpPut("{employeeId:guid}/qualifications/{qualificationId:guid}")]
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
            var qualification = await _service.GetQualificationByIdAsync(qualificationId, cancellationToken);
            if (qualification == null || qualification.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateQualificationAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a qualification from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="qualificationId">The unique identifier of the qualification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Qualification removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Qualification not found.</response>
    [HttpDelete("{employeeId:guid}/qualifications/{qualificationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveQualification(Guid employeeId, Guid qualificationId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (qualificationId == Guid.Empty) return BadRequest("Invalid qualification id.");

        try
        {
            var qualification = await _service.GetQualificationByIdAsync(qualificationId, cancellationToken);
            if (qualification == null || qualification.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveQualificationAsync(qualificationId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Verifies an employee's qualification, marking it as officially confirmed.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="qualificationId">The unique identifier of the qualification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verified qualification.</returns>
    /// <response code="200">Qualification verified successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Qualification not found.</response>
    [HttpPost("{employeeId:guid}/qualifications/{qualificationId:guid}/verify")]
    [ProducesResponseType(typeof(EmployeeQualificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeQualificationDto>> VerifyQualification(Guid employeeId, Guid qualificationId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (qualificationId == Guid.Empty) return BadRequest("Invalid qualification id.");

        try
        {
            var qualification = await _service.GetQualificationByIdAsync(qualificationId, cancellationToken);
            if (qualification == null || qualification.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.VerifyQualificationAsync(qualificationId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes verification status from an employee's qualification.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="qualificationId">The unique identifier of the qualification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unverified qualification.</returns>
    /// <response code="200">Qualification unverified successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Qualification not found.</response>
    [HttpPost("{employeeId:guid}/qualifications/{qualificationId:guid}/unverify")]
    [ProducesResponseType(typeof(EmployeeQualificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeQualificationDto>> UnverifyQualification(Guid employeeId, Guid qualificationId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (qualificationId == Guid.Empty) return BadRequest("Invalid qualification id.");

        try
        {
            var qualification = await _service.GetQualificationByIdAsync(qualificationId, cancellationToken);
            if (qualification == null || qualification.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.UnverifyQualificationAsync(qualificationId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all skills for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employee skills.</returns>
    /// <response code="200">Skills retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/skills")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSkillDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeSkillDto>>> GetSkills(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetSkillsAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Adds a new skill for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The skill data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created skill.</returns>
    /// <response code="201">Skill created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/skills")]
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

    /// <summary>
    /// Updates an existing skill for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeSkillId">The unique identifier of the employee skill.</param>
    /// <param name="dto">The updated skill data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated skill.</returns>
    /// <response code="200">Skill updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Skill not found.</response>
    [HttpPut("{employeeId:guid}/skills/{employeeSkillId:guid}")]
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
            var skill = await _service.GetSkillByIdAsync(employeeSkillId, cancellationToken);
            if (skill == null || skill.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateSkillAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a skill from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeSkillId">The unique identifier of the employee skill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Skill removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Skill not found.</response>
    [HttpDelete("{employeeId:guid}/skills/{employeeSkillId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSkill(Guid employeeId, Guid employeeSkillId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeSkillId == Guid.Empty) return BadRequest("Invalid employee skill id.");

        try
        {
            var skill = await _service.GetSkillByIdAsync(employeeSkillId, cancellationToken);
            if (skill == null || skill.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveSkillAsync(employeeSkillId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Verifies an employee's skill, marking it as officially confirmed.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeSkillId">The unique identifier of the employee skill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verified skill.</returns>
    /// <response code="200">Skill verified successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Skill not found.</response>
    [HttpPost("{employeeId:guid}/skills/{employeeSkillId:guid}/verify")]
    [ProducesResponseType(typeof(EmployeeSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSkillDto>> VerifySkill(Guid employeeId, Guid employeeSkillId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeSkillId == Guid.Empty) return BadRequest("Invalid employee skill id.");

        try
        {
            var skill = await _service.GetSkillByIdAsync(employeeSkillId, cancellationToken);
            if (skill == null || skill.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.VerifySkillAsync(employeeSkillId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes verification status from an employee's skill.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="employeeSkillId">The unique identifier of the employee skill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unverified skill.</returns>
    /// <response code="200">Skill unverified successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Skill not found.</response>
    [HttpPost("{employeeId:guid}/skills/{employeeSkillId:guid}/unverify")]
    [ProducesResponseType(typeof(EmployeeSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSkillDto>> UnverifySkill(Guid employeeId, Guid employeeSkillId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (employeeSkillId == Guid.Empty) return BadRequest("Invalid employee skill id.");

        try
        {
            var skill = await _service.GetSkillByIdAsync(employeeSkillId, cancellationToken);
            if (skill == null || skill.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.UnverifySkillAsync(employeeSkillId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all identification cards for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of identification cards.</returns>
    /// <response code="200">Identification cards retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/identification-cards")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeIdentificationCardListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeIdentificationCardListDto>>> GetIdentificationCards(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetIdentificationCardsAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific identification card by its identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the identification card.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identification card details.</returns>
    /// <response code="200">Identification card found and returned successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Identification card not found.</response>
    [HttpGet("{employeeId:guid}/identification-cards/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeIdentificationCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeIdentificationCardDetailDto?>> GetIdentificationCardById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");

        var item = await _service.GetIdentificationCardByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Adds a new identification card for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The identification card data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created identification card.</returns>
    /// <response code="201">Identification card created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/identification-cards")]
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

    /// <summary>
    /// Updates an existing identification card for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the identification card.</param>
    /// <param name="dto">The updated identification card data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated identification card.</returns>
    /// <response code="200">Identification card updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Identification card not found.</response>
    [HttpPut("{employeeId:guid}/identification-cards/{id:guid}")]
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
            var card = await _service.GetIdentificationCardByIdAsync(id, cancellationToken);
            if (card == null || card.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateIdentificationCardAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes an identification card from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the identification card.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Identification card removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Identification card not found.</response>
    [HttpDelete("{employeeId:guid}/identification-cards/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveIdentificationCard(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");

        try
        {
            var card = await _service.GetIdentificationCardByIdAsync(id, cancellationToken);
            if (card == null || card.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveIdentificationCardAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Verifies an employee's identification card, marking it as officially confirmed.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the identification card.</param>
    /// <param name="request">The verification details including verified date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verified identification card.</returns>
    /// <response code="200">Identification card verified successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Identification card not found.</response>
    [HttpPost("{employeeId:guid}/identification-cards/{id:guid}/verify")]
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
            var card = await _service.GetIdentificationCardByIdAsync(id, cancellationToken);
            if (card == null || card.EmployeeId != employeeId) return NotFound();

            var updated = await _service.VerifyIdentificationCardAsync(id, request.VerifiedDate, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes verification status from an employee's identification card.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the identification card.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unverified identification card.</returns>
    /// <response code="200">Identification card unverified successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Identification card not found.</response>
    [HttpPost("{employeeId:guid}/identification-cards/{id:guid}/unverify")]
    [ProducesResponseType(typeof(EmployeeIdentificationCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeIdentificationCardDetailDto>> UnverifyIdentificationCard(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid identification card id.");

        try
        {
            var card = await _service.GetIdentificationCardByIdAsync(id, cancellationToken);
            if (card == null || card.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UnverifyIdentificationCardAsync(id, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all work history records for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of work history records.</returns>
    /// <response code="200">Work history retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/work-histories")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeWorkHistoryListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeWorkHistoryListDto>>> GetWorkHistories(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetWorkHistoriesAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific work history record by its identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the work history record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work history record details.</returns>
    /// <response code="200">Work history found and returned successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Work history not found.</response>
    [HttpGet("{employeeId:guid}/work-histories/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeWorkHistoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeWorkHistoryDetailDto?>> GetWorkHistoryById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid work history id.");

        var item = await _service.GetWorkHistoryByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Adds a new work history record for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The work history data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created work history record.</returns>
    /// <response code="201">Work history created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/work-histories")]
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

    /// <summary>
    /// Updates an existing work history record for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the work history record.</param>
    /// <param name="dto">The updated work history data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated work history record.</returns>
    /// <response code="200">Work history updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Work history not found.</response>
    [HttpPut("{employeeId:guid}/work-histories/{id:guid}")]
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
            var history = await _service.GetWorkHistoryByIdAsync(id, cancellationToken);
            if (history == null || history.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateWorkHistoryAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a work history record from an employee's profile.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the work history record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Work history removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Work history not found.</response>
    [HttpDelete("{employeeId:guid}/work-histories/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveWorkHistory(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid work history id.");

        try
        {
            var history = await _service.GetWorkHistoryByIdAsync(id, cancellationToken);
            if (history == null || history.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveWorkHistoryAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all employment contracts for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employee contracts.</returns>
    /// <response code="200">Contracts retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/contracts")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeContractDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeContractDetailDto>>> GetContracts(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetContractsAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves the active contract for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active contract, if one exists.</returns>
    /// <response code="200">Active contract retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    /// <response code="404">No active contract found.</response>
    [HttpGet("{employeeId:guid}/contracts/active")]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto?>> GetActiveContract(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");

        var contract = await _service.GetActiveContractAsync(employeeId, cancellationToken);
        return contract == null ? NotFound() : Ok(contract);
    }

    /// <summary>
    /// Adds a new employment contract for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The contract data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created contract.</returns>
    /// <response code="201">Contract created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/contracts")]
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

    /// <summary>
    /// Updates an existing employment contract for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="contractId">The unique identifier of the contract.</param>
    /// <param name="dto">The updated contract data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated contract.</returns>
    /// <response code="200">Contract updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Contract not found.</response>
    [HttpPut("{employeeId:guid}/contracts/{contractId:guid}")]
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
            var contract = await _service.GetContractByIdAsync(contractId, cancellationToken);
            if (contract == null || contract.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateContractAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes an employment contract from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="contractId">The unique identifier of the contract.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Contract removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Contract not found.</response>
    [HttpDelete("{employeeId:guid}/contracts/{contractId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveContract(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");

        try
        {
            var contract = await _service.GetContractByIdAsync(contractId, cancellationToken);
            if (contract == null || contract.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveContractAsync(contractId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Activates an employment contract, making it the active contract.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="contractId">The unique identifier of the contract.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated contract.</returns>
    /// <response code="200">Contract activated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Contract not found.</response>
    [HttpPost("{employeeId:guid}/contracts/{contractId:guid}/activate")]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto>> ActivateContract(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");

        try
        {
            var contract = await _service.GetContractByIdAsync(contractId, cancellationToken);
            if (contract == null || contract.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.ActivateContractAsync(contractId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Deactivates an employment contract, making it temporarily inactive.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="contractId">The unique identifier of the contract.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deactivated contract.</returns>
    /// <response code="200">Contract deactivated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Contract not found.</response>
    [HttpPost("{employeeId:guid}/contracts/{contractId:guid}/deactivate")]
    [ProducesResponseType(typeof(EmployeeContractDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDetailDto>> DeactivateContract(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (contractId == Guid.Empty) return BadRequest("Invalid contract id.");

        try
        {
            var contract = await _service.GetContractByIdAsync(contractId, cancellationToken);
            if (contract == null || contract.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.DeactivateContractAsync(contractId, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Terminates an employment contract with the specified termination details.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="contractId">The unique identifier of the contract.</param>
    /// <param name="request">The termination details including date and reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The terminated contract.</returns>
    /// <response code="200">Contract terminated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Contract not found.</response>
    [HttpPost("{employeeId:guid}/contracts/{contractId:guid}/terminate")]
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
            var contract = await _service.GetContractByIdAsync(contractId, cancellationToken);
            if (contract == null || contract.EmployeeId != employeeId) return NotFound();

            var updated = await _service.TerminateContractAsync(contractId, request.TerminationDate, request.Reason, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all expatriate assignments for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of expatriate assignments.</returns>
    /// <response code="200">Expatriate assignments retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/expatriate-assignments")]
    [ProducesResponseType(typeof(IEnumerable<ExpatriateAssignmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpatriateAssignmentListDto>>> GetExpatriateAssignments(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetExpatriateAssignmentsAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific expatriate assignment by its identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the expatriate assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The expatriate assignment details.</returns>
    /// <response code="200">Expatriate assignment found and returned successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Expatriate assignment not found.</response>
    [HttpGet("{employeeId:guid}/expatriate-assignments/{id:guid}")]
    [ProducesResponseType(typeof(ExpatriateAssignmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpatriateAssignmentDetailDto?>> GetExpatriateAssignmentById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid expatriate assignment id.");

        var item = await _service.GetExpatriateAssignmentByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Adds a new expatriate assignment for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The expatriate assignment data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created expatriate assignment.</returns>
    /// <response code="201">Expatriate assignment created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/expatriate-assignments")]
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

    /// <summary>
    /// Updates an existing expatriate assignment for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the expatriate assignment.</param>
    /// <param name="dto">The updated expatriate assignment data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated expatriate assignment.</returns>
    /// <response code="200">Expatriate assignment updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Expatriate assignment not found.</response>
    [HttpPut("{employeeId:guid}/expatriate-assignments/{id:guid}")]
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
            var assignment = await _service.GetExpatriateAssignmentByIdAsync(id, cancellationToken);
            if (assignment == null || assignment.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateExpatriateAssignmentAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes an expatriate assignment from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the expatriate assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Expatriate assignment removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Expatriate assignment not found.</response>
    [HttpDelete("{employeeId:guid}/expatriate-assignments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveExpatriateAssignment(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid expatriate assignment id.");

        try
        {
            var assignment = await _service.GetExpatriateAssignmentByIdAsync(id, cancellationToken);
            if (assignment == null || assignment.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveExpatriateAssignmentAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all position history records for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of position history records.</returns>
    /// <response code="200">Position history retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/position-histories")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionHistoryListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionHistoryListDto>>> GetPositionHistories(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetPositionHistoryAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific position history record by its identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the position history record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The position history record details.</returns>
    /// <response code="200">Position history found and returned successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Position history not found.</response>
    [HttpGet("{employeeId:guid}/position-histories/{id:guid}")]
    [ProducesResponseType(typeof(EmployeePositionHistoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionHistoryDetailDto?>> GetPositionHistoryById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid position history id.");

        var item = await _service.GetPositionHistoryByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Adds a new position history record for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The position history data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created position history record.</returns>
    /// <response code="201">Position history created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/position-histories")]
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

    /// <summary>
    /// Updates an existing position history record for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the position history record.</param>
    /// <param name="dto">The updated position history data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated position history record.</returns>
    /// <response code="200">Position history updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Position history not found.</response>
    [HttpPut("{employeeId:guid}/position-histories/{id:guid}")]
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
            var history = await _service.GetPositionHistoryByIdAsync(id, cancellationToken);
            if (history == null || history.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdatePositionHistoryAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a position history record from an employee's profile.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the position history record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Position history removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Position history not found.</response>
    [HttpDelete("{employeeId:guid}/position-histories/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePositionHistory(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid position history id.");

        try
        {
            var history = await _service.GetPositionHistoryByIdAsync(id, cancellationToken);
            if (history == null || history.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemovePositionHistoryAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all salary assignments for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of salary assignments.</returns>
    /// <response code="200">Salary assignments retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/salary-assignments")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSalaryAssignmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeSalaryAssignmentListDto>>> GetSalaryAssignments(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetSalaryAssignmentsAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific salary assignment by its identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the salary assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The salary assignment details.</returns>
    /// <response code="200">Salary assignment found and returned successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Salary assignment not found.</response>
    [HttpGet("{employeeId:guid}/salary-assignments/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeSalaryAssignmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeSalaryAssignmentDetailDto?>> GetSalaryAssignmentById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid salary assignment id.");

        var item = await _service.GetSalaryAssignmentByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Assigns a new salary to an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The salary assignment data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created salary assignment.</returns>
    /// <response code="201">Salary assigned successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/salary-assignments")]
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

    /// <summary>
    /// Updates an existing salary assignment for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the salary assignment.</param>
    /// <param name="dto">The updated salary assignment data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated salary assignment.</returns>
    /// <response code="200">Salary assignment updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Salary assignment not found.</response>
    [HttpPut("{employeeId:guid}/salary-assignments/{id:guid}")]
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
            var salary = await _service.GetSalaryAssignmentByIdAsync(id, cancellationToken);
            if (salary == null || salary.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateSalaryAssignmentAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a salary assignment from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the salary assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Salary assignment removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Salary assignment not found.</response>
    [HttpDelete("{employeeId:guid}/salary-assignments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSalaryAssignment(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid salary assignment id.");

        try
        {
            var salary = await _service.GetSalaryAssignmentByIdAsync(id, cancellationToken);
            if (salary == null || salary.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveSalaryAssignmentAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all referees for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employee referees.</returns>
    /// <response code="200">Referees retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/referees")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeRefereeListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeRefereeListDto>>> GetReferees(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetRefereesAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific referee by its identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the referee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The referee details.</returns>
    /// <response code="200">Referee found and returned successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Referee not found.</response>
    [HttpGet("{employeeId:guid}/referees/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto?>> GetRefereeById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        var item = await _service.GetRefereeByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Adds a new referee for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The referee data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created referee.</returns>
    /// <response code="201">Referee created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/referees")]
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

    /// <summary>
    /// Updates an existing referee for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the referee.</param>
    /// <param name="dto">The updated referee data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated referee.</returns>
    /// <response code="200">Referee updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Referee not found.</response>
    [HttpPut("{employeeId:guid}/referees/{id:guid}")]
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
            var referee = await _service.GetRefereeByIdAsync(id, cancellationToken);
            if (referee == null || referee.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateRefereeAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a referee from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the referee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Referee removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Referee not found.</response>
    [HttpDelete("{employeeId:guid}/referees/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            var referee = await _service.GetRefereeByIdAsync(id, cancellationToken);
            if (referee == null || referee.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveRefereeAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Sets a referee as the primary reference for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the referee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated referee marked as primary.</returns>
    /// <response code="200">Primary referee set successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Referee not found.</response>
    [HttpPost("{employeeId:guid}/referees/{id:guid}/set-primary")]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> SetPrimaryReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            var referee = await _service.GetRefereeByIdAsync(id, cancellationToken);
            if (referee == null || referee.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.SetPrimaryRefereeAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Activates a referee, making them available as a reference.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the referee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated referee.</returns>
    /// <response code="200">Referee activated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Referee not found.</response>
    [HttpPost("{employeeId:guid}/referees/{id:guid}/activate")]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> ActivateReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            var referee = await _service.GetRefereeByIdAsync(id, cancellationToken);
            if (referee == null || referee.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.ActivateRefereeAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Deactivates a referee, making them temporarily unavailable as a reference.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the referee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deactivated referee.</returns>
    /// <response code="200">Referee deactivated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Referee not found.</response>
    [HttpPost("{employeeId:guid}/referees/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(EmployeeRefereeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeRefereeDetailDto>> DeactivateReferee(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid referee id.");

        try
        {
            var referee = await _service.GetRefereeByIdAsync(id, cancellationToken);
            if (referee == null || referee.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.DeactivateRefereeAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Retrieves all guarantors for a specific employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of employee guarantors.</returns>
    /// <response code="200">Guarantors retrieved successfully.</response>
    /// <response code="400">Invalid employee identifier provided.</response>
    [HttpGet("{employeeId:guid}/guarantors")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeGuarantorListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeGuarantorListDto>>> GetGuarantors(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetGuarantorsAsync(employeeId, cancellationToken));
    }

    /// <summary>
    /// Retrieves a specific guarantor by its identifier.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guarantor details.</returns>
    /// <response code="200">Guarantor found and returned successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpGet("{employeeId:guid}/guarantors/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto?>> GetGuarantorById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        var item = await _service.GetGuarantorByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Adds a new guarantor for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="dto">The guarantor data to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created guarantor.</returns>
    /// <response code="201">Guarantor created successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    [HttpPost("{employeeId:guid}/guarantors")]
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

    /// <summary>
    /// Updates an existing guarantor for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="dto">The updated guarantor data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated guarantor.</returns>
    /// <response code="200">Guarantor updated successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpPut("{employeeId:guid}/guarantors/{id:guid}")]
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
            var guarantor = await _service.GetGuarantorByIdAsync(id, cancellationToken);
            if (guarantor == null || guarantor.EmployeeId != employeeId) return NotFound();

            var updated = await _service.UpdateGuarantorAsync(dto, cancellationToken);
            return Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes a guarantor from an employee's record.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on successful deletion.</returns>
    /// <response code="204">Guarantor removed successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpDelete("{employeeId:guid}/guarantors/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            var guarantor = await _service.GetGuarantorByIdAsync(id, cancellationToken);
            if (guarantor == null || guarantor.EmployeeId != employeeId) return NotFound();

            var ok = await _service.RemoveGuarantorAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Sets a guarantor as the primary guarantor for an employee.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated guarantor marked as primary.</returns>
    /// <response code="200">Primary guarantor set successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/set-primary")]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> SetPrimaryGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            var guarantor = await _service.GetGuarantorByIdAsync(id, cancellationToken);
            if (guarantor == null || guarantor.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.SetPrimaryGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Verifies an employee's guarantor with official confirmation details.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="request">The verification details including verifier and date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verified guarantor.</returns>
    /// <response code="200">Guarantor verified successfully.</response>
    /// <response code="400">Invalid data provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/verify")]
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
            var guarantor = await _service.GetGuarantorByIdAsync(id, cancellationToken);
            if (guarantor == null || guarantor.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.VerifyGuarantorAsync(id, request.VerifiedByEmployeeId, request.VerifiedDate, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Removes verification status from an employee's guarantor.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unverified guarantor.</returns>
    /// <response code="200">Guarantor unverified successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/unverify")]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> UnverifyGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            var guarantor = await _service.GetGuarantorByIdAsync(id, cancellationToken);
            if (guarantor == null || guarantor.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.UnverifyGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Activates a guarantor, making them available as an active guarantor.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated guarantor.</returns>
    /// <response code="200">Guarantor activated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/activate")]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> ActivateGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            var guarantor = await _service.GetGuarantorByIdAsync(id, cancellationToken);
            if (guarantor == null || guarantor.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.ActivateGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    /// <summary>
    /// Deactivates a guarantor, making them temporarily unavailable.
    /// </summary>
    /// <param name="employeeId">The unique identifier of the employee.</param>
    /// <param name="id">The unique identifier of the guarantor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deactivated guarantor.</returns>
    /// <response code="200">Guarantor deactivated successfully.</response>
    /// <response code="400">Invalid identifier provided.</response>
    /// <response code="404">Guarantor not found.</response>
    [HttpPost("{employeeId:guid}/guarantors/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(EmployeeGuarantorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeGuarantorDetailDto>> DeactivateGuarantor(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid guarantor id.");

        try
        {
            var guarantor = await _service.GetGuarantorByIdAsync(id, cancellationToken);
            if (guarantor == null || guarantor.EmployeeId != employeeId) return NotFound();

            return Ok(await _service.DeactivateGuarantorAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToClientError(ex);
        }
    }

    [HttpGet("{employeeId:guid}/bank-details")]
    public async Task<ActionResult<IEnumerable<EmployeeBankDetailDto>>> GetBankDetails(Guid employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        return Ok(await _service.GetBankDetailsAsync(employeeId, cancellationToken));
    }

    [HttpGet("{employeeId:guid}/bank-details/{id:guid}")]
    public async Task<ActionResult<EmployeeBankDetailDto>> GetBankDetailById(Guid employeeId, Guid id, CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty) return BadRequest("Invalid employee id.");
        if (id == Guid.Empty) return BadRequest("Invalid bank detail id.");
        var result = await _service.GetBankDetailByIdAsync(id, cancellationToken);
        if (result == null || result.EmployeeId != employeeId) return NotFound();
        return Ok(result);
    }

    [HttpPost("{employeeId:guid}/bank-details")]
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
