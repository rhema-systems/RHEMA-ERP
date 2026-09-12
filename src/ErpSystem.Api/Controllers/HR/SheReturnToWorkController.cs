using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

// Gated on the Medical permission policies rather than the SHE HR-role gate: plans carry
// medical restrictions and clearance notes — medical-grade data, per the agreed SHE↔Medical
// boundary. Reads need MedicalRead; writes MedicalWrite; deletes MedicalAdmin.
[ApiController]
[Route("api/safety/return-to-work")]
[SafetyBusinessRules]
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class SheReturnToWorkController : SheApiControllerBase
{
    private readonly ISheReturnToWorkService _service;

    public SheReturnToWorkController(ISheReturnToWorkService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheReturnToWorkPlanSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheReturnToWorkPlanDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{planNumber}")]
    public async Task<ActionResult<SheReturnToWorkPlanDto?>> GetByNumber(string planNumber)
        => Ok(await _service.GetByNumberAsync(planNumber));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SheReturnToWorkPlanSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeAsync(employeeId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SheReturnToWorkPlanSummaryDto>>> GetByStatus(SheReturnToWorkStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("incident/{safetyIncidentId:guid}")]
    public async Task<ActionResult<IEnumerable<SheReturnToWorkPlanSummaryDto>>> GetByIncident(Guid safetyIncidentId)
        => Ok(await _service.GetByIncidentAsync(safetyIncidentId));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<SheReturnToWorkPlanSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpPost]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheReturnToWorkPlanDto>> Create([FromBody] CreateSheReturnToWorkPlanDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheReturnToWorkPlanDto>> Update(Guid id, [FromBody] UpdateSheReturnToWorkPlanDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Phases ──
    [HttpPost("{id:guid}/phases")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheReturnToWorkPhaseDto>> AddPhase(Guid id, [FromBody] CreateSheReturnToWorkPhaseDto dto)
    {
        dto.ReturnToWorkPlanId = id;
        return Ok(await _service.AddPhaseAsync(dto, TenantId, UserId));
    }

    [HttpPut("phases/{phaseId:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheReturnToWorkPhaseDto>> UpdatePhase(Guid phaseId, [FromBody] UpdateSheReturnToWorkPhaseDto dto)
    {
        if (phaseId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdatePhaseAsync(dto, UserId));
    }

    [HttpDelete("phases/{phaseId:guid}")]
    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    public async Task<IActionResult> DeletePhase(Guid phaseId)
    {
        await _service.DeletePhaseAsync(phaseId);
        return NoContent();
    }

    // ── Reviews ──
    [HttpGet("{id:guid}/reviews")]
    public async Task<ActionResult<IEnumerable<SheReturnToWorkReviewDto>>> GetReviews(Guid id)
        => Ok(await _service.GetReviewsAsync(id));

    [HttpPost("{id:guid}/reviews")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheReturnToWorkReviewDto>> AddReview(Guid id, [FromBody] CreateSheReturnToWorkReviewDto dto)
    {
        dto.ReturnToWorkPlanId = id;
        return Ok(await _service.AddReviewAsync(dto, TenantId, UserId));
    }
}
