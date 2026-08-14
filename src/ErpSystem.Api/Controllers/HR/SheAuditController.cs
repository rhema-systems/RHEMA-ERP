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
/// SHE audit management (FRD §12 / FR-SHE-229): planning → execution → findings →
/// CAPA → verification → closure. HR-gated end to end — audit records are
/// governance artefacts with no employee self-service surface.
/// </summary>
[ApiController]
[Route("api/safety/audits")]
[SafetyBusinessRules]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class SheAuditController : SheApiControllerBase
{
    private readonly ISheAuditService _service;

    public SheAuditController(ISheAuditService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── audits ──
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheAuditSummaryDto>>> GetAll(
        [FromQuery] SheAuditStatus? status, [FromQuery] int? year)
        => Ok(await _service.GetAllAsync(status, year));

    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<SheAuditSummaryDto>>> GetUpcoming([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetUpcomingAsync(daysAhead));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheAuditDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{auditNumber}")]
    public async Task<ActionResult<SheAuditDto?>> GetByNumber(string auditNumber)
        => Ok(await _service.GetByNumberAsync(auditNumber));

    [HttpPost]
    public async Task<ActionResult<SheAuditDto>> Create([FromBody] CreateSheAuditDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SheAuditDto>> Update(Guid id, [FromBody] UpdateSheAuditDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<SheAuditDto>> Start(Guid id, [FromBody] StartSheAuditDto dto)
    {
        dto.AuditId = id;
        return Ok(await _service.StartAsync(dto, UserId));
    }

    [HttpPost("{id:guid}/issue-report")]
    public async Task<ActionResult<SheAuditDto>> IssueReport(Guid id, [FromBody] IssueSheAuditReportDto dto)
    {
        dto.AuditId = id;
        return Ok(await _service.IssueReportAsync(dto, UserId));
    }

    /// <summary>Refused (422) while any finding remains unclosed.</summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<SheAuditDto>> Close(Guid id, [FromBody] CloseSheAuditDto dto)
    {
        dto.AuditId = id;
        return Ok(await _service.CloseAsync(dto, UserId));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SheAuditDto>> Cancel(Guid id, [FromBody] CancelSheAuditDto dto)
    {
        dto.AuditId = id;
        return Ok(await _service.CancelAsync(dto, UserId));
    }

    /// <summary>Planned/Cancelled audits only — executed audits are evidence and refuse deletion.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── team ──
    [HttpPost("{id:guid}/team")]
    public async Task<ActionResult<SheAuditTeamMemberDto>> AddTeamMember(Guid id, [FromBody] CreateSheAuditTeamMemberDto dto)
    {
        dto.AuditId = id;
        return Ok(await _service.AddTeamMemberAsync(dto, TenantId, UserId));
    }

    [HttpDelete("team/{teamMemberId:guid}")]
    public async Task<IActionResult> RemoveTeamMember(Guid teamMemberId)
    {
        await _service.RemoveTeamMemberAsync(teamMemberId);
        return NoContent();
    }

    // ── findings ──
    /// <summary>Findings are recorded while the audit is InProgress; numbers are server-assigned per audit.</summary>
    [HttpPost("{id:guid}/findings")]
    public async Task<ActionResult<SheAuditFindingDto>> AddFinding(Guid id, [FromBody] CreateSheAuditFindingDto dto)
    {
        dto.AuditId = id;
        return Ok(await _service.AddFindingAsync(dto, TenantId, UserId));
    }

    [HttpPut("findings/{findingId:guid}")]
    public async Task<ActionResult<SheAuditFindingDto>> UpdateFinding(Guid findingId, [FromBody] UpdateSheAuditFindingDto dto)
    {
        if (findingId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateFindingAsync(dto, UserId));
    }

    /// <summary>Effectiveness verification — only a Resolved finding verifies.</summary>
    [HttpPost("findings/{findingId:guid}/verify")]
    public async Task<ActionResult<SheAuditFindingDto>> VerifyFinding(Guid findingId, [FromBody] VerifySheAuditFindingDto dto)
    {
        dto.FindingId = findingId;
        return Ok(await _service.VerifyFindingAsync(dto, UserId));
    }

    /// <summary>Refused (422) until the finding is Verified and its corrective actions are complete.</summary>
    [HttpPost("findings/{findingId:guid}/close")]
    public async Task<ActionResult<SheAuditFindingDto>> CloseFinding(Guid findingId)
        => Ok(await _service.CloseFindingAsync(findingId, UserId));

    // ── finding actions (the unified CA tracker's fifth source) ──
    [HttpPost("findings/{findingId:guid}/actions")]
    public async Task<ActionResult<SheAuditFindingActionDto>> AddFindingAction(Guid findingId, [FromBody] CreateSheAuditFindingActionDto dto)
    {
        dto.FindingId = findingId;
        return Ok(await _service.AddFindingActionAsync(dto, TenantId, UserId));
    }

    [HttpPut("finding-actions/{actionId:guid}")]
    public async Task<ActionResult<SheAuditFindingActionDto>> UpdateFindingAction(Guid actionId, [FromBody] UpdateSheAuditFindingActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateFindingActionAsync(dto, UserId));
    }
}
