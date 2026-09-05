using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The employee-relations responder matrix — area 9c slice 5, and <b>FR-HR-084</b>: "model a
/// grievance hierarchy defining reporting lines."
/// </summary>
/// <remarks>
/// <para><b>Setup data, maintained by HR on a screen, and that is the decision rather than a
/// shortcut.</b> FR-HR-181's Supervisor and HOD rungs would naturally resolve from
/// <c>Employees.ManagerId</c> and <c>OrganizationUnits.HeadEmployeeId</c>. Measured on the DEFAULT
/// tenant on 2026-08-27 those are populated for <b>486 of 8,353 employees (5.8%)</b> and <b>2 of 48
/// units</b> — both worse than six weeks earlier. A derived hierarchy resolves to nobody for 94% of
/// staff.</para>
///
/// <para>This is the fourth requirement to hit that wall (FR-HR-080, FR-HR-173, FR-HR-181, now
/// FR-HR-084), and rather than work around it a fourth time slice 5 builds the alternative already
/// put to TDC in <c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c> §4.</para>
///
/// <para>⚠ <b>This matrix is also what would unblock FR-HR-080</b> — the rule that a head of
/// department may issue only verbal warnings, which area 9 built correctly and left inert because
/// the system could not tell who a head of department was. Opening that is a gate change in the
/// discipline area, not a rule change; noted here for whoever reopens it.</para>
///
/// <para>The route sits under <c>api/hr/employee-relations/responders</c>. It does not collide with
/// the case controller's <c>{id:guid}</c> routes because "responders" is not a GUID.</para>
/// </remarks>
[ApiController]
[Route("api/hr/employee-relations/responders")]
[Authorize(Policy = "InternalOnly")]
[DisciplineBusinessRules]
public class EmployeeRelationsRespondersController : ControllerBase
{
    private readonly IEmployeeRelationsResponderService _service;

    public EmployeeRelationsRespondersController(IEmployeeRelationsResponderService service)
        => _service = service;

    /// <summary>Every row in the matrix, tenant-wide defaults first.</summary>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmployeeRelationsResponderDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>The rows for one scope. Omit the unit for the tenant-wide defaults.</summary>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("scope")]
    public async Task<ActionResult<IEnumerable<EmployeeRelationsResponderDto>>> GetForUnit(
        [FromQuery] Guid? organizationUnitId = null)
        => Ok(await _service.GetForUnitAsync(organizationUnitId));

    /// <summary>
    /// What the matrix resolves to for a unit and a rung, right now, without filing anything.
    /// </summary>
    /// <remarks>
    /// ⚠ An unmatched lookup answers <b>200</b> with <c>resolved: false</c>, not 404. Showing HR
    /// where the matrix resolves to nobody is the admin screen's main job, and a lookup that
    /// errored could not be rendered as a gap.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("resolve")]
    public async Task<ActionResult<ResponderResolutionDto>> Resolve(
        [FromQuery] GrievanceEscalationLevel level,
        [FromQuery] Guid? organizationUnitId = null)
        => Ok(await _service.ResolveAsync(organizationUnitId, level));

    /// <summary>Every rung's answer for one unit — the coverage row the admin screen renders.</summary>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("coverage")]
    public async Task<ActionResult<ResponderCoverageDto>> GetCoverage(
        [FromQuery] Guid? organizationUnitId = null)
        => Ok(await _service.GetCoverageAsync(organizationUnitId));

    /// <summary>
    /// Names a responder for a rung and a scope.
    /// </summary>
    /// <remarks>
    /// Refuses a period that overlaps an existing assignment for the same scope and rung: two people
    /// answering one rung for one unit on one day would resolve arbitrarily.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<EmployeeRelationsResponderDto>> Create(
        [FromBody] UpsertEmployeeRelationsResponderDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), new { }, created);
    }

    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeRelationsResponderDto>> Update(
        Guid id, [FromBody] UpsertEmployeeRelationsResponderDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateAsync(id, dto));
    }

    /// <summary>
    /// Removes an assignment. A soft delete — who answered a rung last year is a fact about the
    /// cases decided last year, and the matrix is read by the audit trail as well as by the router.
    /// </summary>
    [Authorize(Policy = HrPermissions.DisciplineAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
