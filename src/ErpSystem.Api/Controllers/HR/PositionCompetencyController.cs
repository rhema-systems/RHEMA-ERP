using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/position-competencies")]
[Authorize(Policy = "InternalOnly")]
public class PositionCompetencyController : ControllerBase
{
    private readonly IPositionCompetencyService _service;
    private readonly ICurrentUserService _currentUser;

    public PositionCompetencyController(
        IPositionCompetencyService service,
        ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PositionCompetencyDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<PositionCompetencyDto>>> GetByPosition(Guid positionId)
        => Ok(await _service.GetByPositionIdAsync(positionId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("competency/{competencyId:guid}")]
    public async Task<ActionResult<IEnumerable<PositionCompetencyDto>>> GetByCompetency(Guid competencyId)
        => Ok(await _service.GetByCompetencyIdAsync(competencyId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("position/{positionId:guid}/competency/{competencyId:guid}")]
    public async Task<ActionResult<PositionCompetencyDto?>> GetByPair(Guid positionId, Guid competencyId)
        => Ok(await _service.GetByPositionAndCompetencyAsync(positionId, competencyId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<PositionCompetencyDto>> Create([FromBody] CreatePositionCompetencyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PositionCompetencyDto>> Update(Guid id, [FromBody] UpdatePositionCompetencyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.CompetencyAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // BULK SET
    // =========================================================================

    /// <summary>
    /// Atomically replaces the complete competency requirement set for a position.
    /// Any existing requirements not present in the payload are soft-deleted.
    /// </summary>
    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
    [HttpPut("position/{positionId:guid}/bulk-set")]
    public async Task<ActionResult<IEnumerable<PositionCompetencyDto>>> BulkSet(
        Guid positionId, [FromBody] BulkSetPositionCompetenciesDto dto)
    {
        if (positionId != dto.PositionId) return BadRequest("Position ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var result = await _service.BulkSetForPositionAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(result);
    }
}
