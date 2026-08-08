using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/risk-assessments")]
[Authorize]
public class SheRiskAssessmentController : SheApiControllerBase
{
    private readonly ISheRiskAssessmentService _service;

    public SheRiskAssessmentController(ISheRiskAssessmentService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheRiskAssessmentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{assessmentNumber}")]
    public async Task<ActionResult<SheRiskAssessmentDto?>> GetByNumber(string assessmentNumber)
        => Ok(await _service.GetByNumberAsync(assessmentNumber));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetByStatus(SheRiskAssessmentStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetByType(SheRiskAssessmentType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("preparer/{preparedById:guid}")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetByPreparer(Guid preparedById)
        => Ok(await _service.GetByPreparerAsync(preparedById));

    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetExpiring([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringAsync(daysAhead));

    [HttpGet("due-for-review")]
    public async Task<ActionResult<IEnumerable<SheRiskAssessmentSummaryDto>>> GetDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForReviewAsync(daysAhead));

    [HttpPost]
    public async Task<ActionResult<SheRiskAssessmentDto>> Create([FromBody] CreateSheRiskAssessmentDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SheRiskAssessmentDto>> Update(Guid id, [FromBody] UpdateSheRiskAssessmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSheRiskAssessmentDto dto)
    {
        dto.RiskAssessmentId = id;
        await _service.ApproveAsync(dto, UserId);
        return Ok(new { message = "Risk assessment approved." });
    }

    // ── Assessed hazards ──
    [HttpPost("{id:guid}/hazards")]
    public async Task<ActionResult<SheRiskAssessmentHazardDto>> AddHazard(Guid id, [FromBody] CreateSheRiskAssessmentHazardDto dto)
    {
        dto.RiskAssessmentId = id;
        return Ok(await _service.AddHazardAsync(dto, TenantId, UserId));
    }

    [HttpPut("hazards/{hazardLineId:guid}")]
    public async Task<ActionResult<SheRiskAssessmentHazardDto>> UpdateHazard(Guid hazardLineId, [FromBody] UpdateSheRiskAssessmentHazardDto dto)
    {
        if (hazardLineId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateHazardAsync(dto, UserId));
    }

    [HttpDelete("hazards/{hazardLineId:guid}")]
    public async Task<IActionResult> DeleteHazard(Guid hazardLineId)
    {
        await _service.DeleteHazardAsync(hazardLineId);
        return NoContent();
    }

    // ── Acknowledgements ──
    [HttpPost("{id:guid}/acknowledgements")]
    public async Task<ActionResult<SheRiskAssessmentAcknowledgementDto>> AddAcknowledgement(Guid id, [FromBody] CreateSheRiskAssessmentAcknowledgementDto dto)
    {
        dto.RiskAssessmentId = id;
        return Ok(await _service.AddAcknowledgementAsync(dto, TenantId, UserId));
    }

    [HttpGet("for-acknowledgement/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<MyRiskAcknowledgementDto>>> GetForAcknowledgement(Guid employeeId)
        => Ok(await _service.GetForEmployeeAcknowledgementAsync(employeeId));
}
