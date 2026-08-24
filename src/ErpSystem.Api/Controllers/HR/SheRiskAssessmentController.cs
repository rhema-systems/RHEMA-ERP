using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Risk assessments (HIRA/JSA) — HR only, with one exception: any authenticated employee can sign
/// an acknowledgement of a risk assessment (FR-SHE-030 family), and the signature is stamped from
/// the token's employee — the body cannot sign on someone else's behalf unless the caller is HR.
/// The employee-facing "what do I still need to acknowledge" read is deliberately deferred to the
/// employee-portal work (area 25); the HR-gated per-employee variant here serves the HR screens.
/// </summary>
[ApiController]
[SafetyBusinessRules]
[Route("api/safety/risk-assessments")]
[Authorize(Policy = "InternalOnly")]
public class SheRiskAssessmentController : SheApiControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement could not be relaxed for the one acknowledgement action employees need.
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly ISheRiskAssessmentService _service;

    public SheRiskAssessmentController(ISheRiskAssessmentService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [Authorize(Roles = HrRoles)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheRiskAssessmentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("number/{assessmentNumber}")]
    public async Task<ActionResult<SheRiskAssessmentDto?>> GetByNumber(string assessmentNumber)
        => Ok(await _service.GetByNumberAsync(assessmentNumber));

    [Authorize(Roles = HrRoles)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetByStatus(SheRiskAssessmentStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [Authorize(Roles = HrRoles)]
    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetByType(SheRiskAssessmentType type)
        => Ok(await _service.GetByTypeAsync(type));

    [Authorize(Roles = HrRoles)]
    [HttpGet("preparer/{preparedById:guid}")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetByPreparer(Guid preparedById)
        => Ok(await _service.GetByPreparerAsync(preparedById));

    [Authorize(Roles = HrRoles)]
    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetExpiring([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringAsync(daysAhead));

    [Authorize(Roles = HrRoles)]
    [HttpGet("due-for-review")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForReviewAsync(daysAhead));

    [Authorize(Roles = HrRoles)]
    [HttpPost]
    public async Task<ActionResult<SheRiskAssessmentDto>> Create([FromBody] CreateSheRiskAssessmentDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SheRiskAssessmentDto>> Update(Guid id, [FromBody] UpdateSheRiskAssessmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSheRiskAssessmentDto dto)
    {
        dto.RiskAssessmentId = id;
        await _service.ApproveAsync(dto, UserId);
        return Ok(new { message = "Risk assessment approved." });
    }

    // ── Assessed hazards ──
    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/hazards")]
    public async Task<ActionResult<SheRiskAssessmentHazardDto>> AddHazard(Guid id, [FromBody] CreateSheRiskAssessmentHazardDto dto)
    {
        dto.RiskAssessmentId = id;
        return Ok(await _service.AddHazardAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("hazards/{hazardLineId:guid}")]
    public async Task<ActionResult<SheRiskAssessmentHazardDto>> UpdateHazard(Guid hazardLineId, [FromBody] UpdateSheRiskAssessmentHazardDto dto)
    {
        if (hazardLineId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateHazardAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("hazards/{hazardLineId:guid}")]
    public async Task<IActionResult> DeleteHazard(Guid hazardLineId)
    {
        await _service.DeleteHazardAsync(hazardLineId);
        return NoContent();
    }

    // ── Acknowledgements ──
    /// <summary>Open to any authenticated employee — signing is self-service. Non-HR callers always
    /// sign as themselves: the employee comes from the token, never the body.</summary>
    [HttpPost("{id:guid}/acknowledgements")]
    public async Task<ActionResult<SheRiskAssessmentAcknowledgementDto>> AddAcknowledgement(Guid id, [FromBody] CreateSheRiskAssessmentAcknowledgementDto dto)
    {
        dto.RiskAssessmentId = id;
        var isHr = User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);
        if (!isHr || dto.EmployeeId == Guid.Empty)
            dto.EmployeeId = UserId;
        return Ok(await _service.AddAcknowledgementAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpGet("for-acknowledgement/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<MyRiskAcknowledgementDto>>> GetForAcknowledgement(Guid employeeId)
        => Ok(await _service.GetForEmployeeAcknowledgementAsync(employeeId));
}
