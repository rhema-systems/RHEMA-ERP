using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-career-paths")]
[Authorize(Policy = "InternalOnly")]
[MovementBusinessRules]
public class EmployeeCareerPathController : ControllerBase
{
    private readonly IEmployeeCareerPathService _service;
    private readonly ICurrentUserService _currentUser;

    public EmployeeCareerPathController(
        IEmployeeCareerPathService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<EmployeeCareerPathDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{id:guid}/details")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<EmployeeCareerPathDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithDetailsAsync(id));

    [HttpGet("employee/{employeeId:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeCareerPathSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("employee/{employeeId:guid}/current")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<EmployeeCareerPathDto?>> GetCurrentPosition(Guid employeeId)
        => Ok(await _service.GetCurrentPositionAsync(employeeId));

    [HttpGet("org-unit/{organizationUnitId:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeCareerPathSummaryDto>>> GetByOrgUnit(Guid organizationUnitId)
        => Ok(await _service.GetByOrganizationUnitIdAsync(organizationUnitId));

    [HttpGet("org-unit/{organizationUnitId:guid}/current-occupants")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeCareerPathSummaryDto>>> GetCurrentOccupants(Guid organizationUnitId)
        => Ok(await _service.GetCurrentOccupantsForUnitAsync(organizationUnitId));

    [HttpGet("movement/{movementId:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<EmployeeCareerPathDto?>> GetByMovement(Guid movementId)
        => Ok(await _service.GetByMovementIdAsync(movementId));

    [HttpGet("salary-grade/{salaryGradeId:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeCareerPathSummaryDto>>> GetBySalaryGrade(Guid salaryGradeId)
        => Ok(await _service.GetBySalaryGradeIdAsync(salaryGradeId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<ActionResult<EmployeeCareerPathDto>> Create([FromBody] CreateEmployeeCareerPathDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<ActionResult<EmployeeCareerPathDto>> Update(Guid id, [FromBody] UpdateEmployeeCareerPathDto dto)
    {
        if (id != dto.Id)        return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.MovementsAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
