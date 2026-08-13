using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/regulatory")]
[SafetyBusinessRules]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class SheRegulatoryComplianceController : SheApiControllerBase
{
    private readonly ISheRegulatoryComplianceService _service;

    public SheRegulatoryComplianceController(ISheRegulatoryComplianceService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet("obligations")]
    public async Task<ActionResult<IEnumerable<SheRegulatoryObligationSummaryDto>>> GetAllObligations()
        => Ok(await _service.GetAllObligationsAsync());

    [HttpGet("obligations/{id:guid}")]
    public async Task<ActionResult<SheRegulatoryObligationDto>> GetObligation(Guid id)
        => Ok(await _service.GetObligationAsync(id));

    [HttpGet("obligations/code/{obligationCode}")]
    public async Task<ActionResult<SheRegulatoryObligationDto?>> GetObligationByCode(string obligationCode)
        => Ok(await _service.GetObligationByCodeAsync(obligationCode));

    [HttpGet("obligations/domain/{domain}")]
    public async Task<ActionResult<IEnumerable<SheRegulatoryObligationSummaryDto>>> GetObligationsByDomain(SheRegulatoryDomain domain)
        => Ok(await _service.GetObligationsByDomainAsync(domain));

    [HttpGet("obligations/status/{status}")]
    public async Task<ActionResult<IEnumerable<SheRegulatoryObligationSummaryDto>>> GetObligationsByStatus(SheComplianceStatus status)
        => Ok(await _service.GetObligationsByStatusAsync(status));

    [HttpGet("obligations/owner/{ownerId:guid}")]
    public async Task<ActionResult<IEnumerable<SheRegulatoryObligationSummaryDto>>> GetObligationsByOwner(Guid ownerId)
        => Ok(await _service.GetObligationsByOwnerAsync(ownerId));

    [HttpGet("obligations/due-for-review")]
    public async Task<ActionResult<IEnumerable<SheRegulatoryObligationSummaryDto>>> GetObligationsDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetObligationsDueForReviewAsync(daysAhead));

    [HttpGet("obligations/non-compliant")]
    public async Task<ActionResult<IEnumerable<SheRegulatoryObligationSummaryDto>>> GetNonCompliantObligations()
        => Ok(await _service.GetNonCompliantObligationsAsync());

    [HttpPost("obligations")]
    public async Task<ActionResult<SheRegulatoryObligationDto>> CreateObligation([FromBody] CreateSheRegulatoryObligationDto dto)
    {
        var created = await _service.CreateObligationAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetObligation), new { id = created.Id }, created);
    }

    [HttpPut("obligations/{id:guid}")]
    public async Task<ActionResult<SheRegulatoryObligationDto>> UpdateObligation(Guid id, [FromBody] UpdateSheRegulatoryObligationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateObligationAsync(dto, UserId));
    }

    [HttpDelete("obligations/{id:guid}")]
    public async Task<IActionResult> DeleteObligation(Guid id)
    {
        await _service.DeleteObligationAsync(id);
        return NoContent();
    }

    // ── Evidence ──
    [HttpPost("obligations/{id:guid}/evidence")]
    public async Task<ActionResult<SheRegulatoryComplianceEvidenceDto>> AddEvidence(Guid id, [FromBody] CreateSheRegulatoryComplianceEvidenceDto dto)
    {
        dto.ObligationId = id;
        return Ok(await _service.AddEvidenceAsync(dto, TenantId, UserId));
    }

    [HttpDelete("evidence/{evidenceId:guid}")]
    public async Task<IActionResult> DeleteEvidence(Guid evidenceId)
    {
        await _service.DeleteEvidenceAsync(evidenceId);
        return NoContent();
    }
}
