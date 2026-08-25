using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

// ============================================================================
// STAFF OFFENSE CONTROLLER
// ============================================================================

/// <summary>
/// Manages the StaffOffense master-data catalog and its child procedure steps.
///
/// Gated at class level: this is setup data with no employee-facing action on it. The catalog names
/// what counts as misconduct and what the procedure for it is, so it is HR's to maintain.
/// </summary>
[ApiController]
[Route("api/discipline/offenses")]
[Authorize(Policy = "InternalOnly")]
[DisciplineBusinessRules]
public class StaffOffenseController : ControllerBase
{
    private readonly IStaffOffenseService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffOffenseController(IStaffOffenseService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // ── Offense queries ───────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffOffenseSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffOffenseSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<StaffOffenseDto?>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{id:guid}/with-procedures")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<StaffOffenseDto?>> GetWithProcedures(Guid id)
        => Ok(await _service.GetWithProceduresAsync(id));

    [HttpGet("code/{code}")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<StaffOffenseDto?>> GetByCode(string code)
        => Ok(await _service.GetByCodeAsync(code));

    // ── Offense CRUD ──────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<StaffOffenseDto>> Create([FromBody] CreateStaffOffenseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<StaffOffenseDto>> Update(Guid id, [FromBody] UpdateStaffOffenseDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Procedure steps ───────────────────────────────────────────────────────

    [HttpGet("{offenseId:guid}/procedures")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffOffenseProcedureDto>>> GetProcedures(Guid offenseId)
        => Ok(await _service.GetProceduresByOffenseAsync(offenseId));

    [HttpGet("procedures/{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<StaffOffenseProcedureDto?>> GetProcedureById(Guid id)
        => Ok(await _service.GetProcedureByIdAsync(id));

    [HttpPost("{offenseId:guid}/procedures")]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<StaffOffenseProcedureDto>> AddProcedure(
        Guid offenseId, [FromBody] CreateStaffOffenseProcedureDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.OffenseId = offenseId;
        var created = await _service.AddProcedureAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetProcedureById), new { id = created.Id }, created);
    }

    [HttpPut("procedures/{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<StaffOffenseProcedureDto>> UpdateProcedure(
        Guid id, [FromBody] UpdateStaffOffenseProcedureDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateProcedureAsync(dto, employeeId.Value));
    }

    [HttpDelete("procedures/{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineAdminPolicy)]
    public async Task<IActionResult> DeleteProcedure(Guid id)
    {
        await _service.DeleteProcedureAsync(id);
        return NoContent();
    }

    [HttpPost("{offenseId:guid}/procedures/reorder")]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<IEnumerable<StaffOffenseProcedureDto>>> ReorderProcedures(
        Guid offenseId,
        [FromBody] ReorderStaffOffenseProceduresDto dto)
    {
        if (offenseId != dto.OffenseId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var updated = await _service.ReorderProceduresAsync(
            offenseId,
            dto.OrderedProcedureIds,
            employeeId.Value);

        return Ok(updated);
    }
}

// ============================================================================
// STAFF DISCIPLINARY ACTION TYPE CONTROLLER
// ============================================================================

/// <summary>
/// Manages the lookup table of disciplinary action types
/// (e.g., Verbal Warning, Written Warning, Dismissal).
///
/// Gated at class level, as setup data. This catalog is where FR-HR-080's authority limit will hang
/// when the workflow slice lands — which sanction each role may issue — so it is HR's to maintain.
/// </summary>
[ApiController]
[Route("api/discipline/action-types")]
[Authorize(Policy = "InternalOnly")]
[DisciplineBusinessRules]
public class StaffDisciplinaryActionTypeController : ControllerBase
{
    private readonly IStaffDisciplinaryActionTypeService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffDisciplinaryActionTypeController(
        IStaffDisciplinaryActionTypeService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionTypeSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionTypeSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<StaffDisciplinaryActionTypeDto?>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("code/{code}")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<StaffDisciplinaryActionTypeDto?>> GetByCode(string code)
        => Ok(await _service.GetByCodeAsync(code));

    [HttpPost]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<StaffDisciplinaryActionTypeDto>> Create(
        [FromBody] CreateStaffDisciplinaryActionTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<StaffDisciplinaryActionTypeDto>> Update(
        Guid id, [FromBody] UpdateStaffDisciplinaryActionTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.DisciplineAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
