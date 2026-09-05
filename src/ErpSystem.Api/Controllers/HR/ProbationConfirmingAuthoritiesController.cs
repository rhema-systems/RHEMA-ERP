using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The confirming-authority map — who signs off probation for a given unit and staff category.
/// </summary>
/// <remarks>
/// <para>FR-HR-032 routes the month-5 confirmation form to "the head". Measured on the reference
/// tenant, 0 of 41 organisation units carry a head and 7% of employees a manager, so the authority
/// is <b>named here</b> rather than derived (decision D-2, 2026-08-18). Most specific rule wins:
/// unit + level, then unit, then level, then a tenant-wide default.</para>
///
/// <para>Admin-gated throughout, reads included. This map decides who may end someone's probation,
/// so it is administration in the same sense the outcome decisions are — not HR record-keeping.</para>
/// </remarks>
[ApiController]
[Route("api/probations/confirming-authorities")]
[Authorize(Policy = HrPermissions.ProbationAdminPolicy)]
public class ProbationConfirmingAuthoritiesController : ControllerBase
{
    private readonly IProbationConfirmingAuthorityService _service;

    public ProbationConfirmingAuthoritiesController(IProbationConfirmingAuthorityService service)
    {
        _service = service;
    }

    /// <summary>Every rule, most specific first — the order resolution applies them in.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProbationConfirmingAuthorityDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProbationConfirmingAuthorityDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    /// <summary>Who confirms this employee's probation, and which rule decided it.</summary>
    [HttpGet("resolve/{employeeId:guid}")]
    public async Task<ActionResult<ResolvedConfirmingAuthorityDto>> Resolve(Guid employeeId)
        => Ok(await _service.ResolveForEmployeeAsync(employeeId));

    [HttpPost]
    public async Task<ActionResult<ProbationConfirmingAuthorityDto>> Create(
        [FromBody] CreateProbationConfirmingAuthorityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProbationConfirmingAuthorityDto>> Update(
        Guid id, [FromBody] UpdateProbationConfirmingAuthorityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateAsync(id, dto));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
