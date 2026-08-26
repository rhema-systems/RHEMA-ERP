using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Incident register and investigation lifecycle — HR only, with one exception: any authenticated
/// employee can report an incident (FR-SHE-100 / FR-ENV-025 employee reporting), and non-HR
/// reporters are always recorded as themselves — the reporter comes from the token, never the
/// body. Everything else — register reads, investigation, closure, involved persons, witnesses,
/// corrective actions — is HR-gated. The one employee self-service read ("incidents I reported /
/// am involved in") is <c>GET mine</c>, delivered with the employee portal (area 25).
/// </summary>
[ApiController]
[SafetyBusinessRules]
[Route("api/safety/incidents")]
[Authorize(Policy = "InternalOnly")]
public class SafetyIncidentController : SheApiControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement could not be relaxed for the one report action employees need.
    private readonly ISafetyIncidentService _service;

    public SafetyIncidentController(ISafetyIncidentService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Queries ──
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<SafetyIncidentSummaryDto>>> GetPaged(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] SheIncidentStatus? status = null)
        => Ok(await _service.GetPagedAsync(page, pageSize, status));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyIncidentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("number/{incidentNumber}")]
    public async Task<ActionResult<SafetyIncidentDto?>> GetByNumber(string incidentNumber)
        => Ok(await _service.GetByNumberAsync(incidentNumber));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByStatus(SheIncidentStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("severity/{severity}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetBySeverity(SheIncidentSeverity severity)
        => Ok(await _service.GetBySeverityAsync(severity));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByCategory(SheIncidentCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetByDateRangeAsync(from, to));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByInvolvedEmployee(Guid employeeId)
        => Ok(await _service.GetByInvolvedEmployeeAsync(employeeId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("for-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetForEmployee(Guid employeeId)
        => Ok(await _service.GetForEmployeeAsync(employeeId));

    /// <summary>Incidents the caller reported or was recorded as an involved person in — the
    /// employee-portal read the class doc defers to area 25, delivered there. Summary rows only;
    /// the employee always resolved from the token, no SHE permission required.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetMine()
        => Ok(await _service.GetForEmployeeAsync(UserId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("requiring-investigation")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetRequiringInvestigation()
        => Ok(await _service.GetRequiringInvestigationAsync());

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("open")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetOpen()
        => Ok(await _service.GetOpenAsync());

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("lost-time")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetLostTimeInjuries()
        => Ok(await _service.GetLostTimeInjuriesAsync());

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("reportable-pending")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetReportableNotYetNotified()
        => Ok(await _service.GetReportableNotYetNotifiedAsync());

    // ── CRUD ──
    /// <summary>Open to any authenticated employee — incident reporting must not be gatekept. Non-HR
    /// reporters always report as themselves: the reporter comes from the token, never the body.</summary>
    [HttpPost]
    public async Task<ActionResult<SafetyIncidentDto>> Create([FromBody] CreateSafetyIncidentDto dto)
    {
        // W3 slice 12: the on-behalf arm is the desk tier, not the HR role.
        var isDesk = await HoldsPolicyAsync(HrPermissions.SheWritePolicy);
        if (!isDesk || dto.ReportedById == Guid.Empty)
            dto.ReportedById = UserId;
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetyIncidentDto>> Update(Guid id, [FromBody] UpdateSafetyIncidentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Workflow ──
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/assign-investigation")]
    public async Task<IActionResult> AssignInvestigation(Guid id, [FromBody] AssignSafetyIncidentInvestigationDto dto)
    {
        dto.IncidentId = id;
        await _service.AssignInvestigationAsync(dto, UserId);
        return Ok(new { message = "Investigation assigned." });
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/record-investigation")]
    public async Task<IActionResult> RecordInvestigation(Guid id, [FromBody] RecordSafetyIncidentInvestigationDto dto)
    {
        dto.IncidentId = id;
        await _service.RecordInvestigationAsync(dto, UserId);
        return Ok(new { message = "Investigation findings recorded." });
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/notify-authority")]
    public async Task<IActionResult> NotifyAuthority(Guid id, [FromBody] NotifySafetyIncidentAuthorityDto dto)
    {
        dto.IncidentId = id;
        await _service.NotifyAuthorityAsync(dto, UserId);
        return Ok(new { message = "Authority notification recorded." });
    }

    // ── Statutory submissions (slice 15, FR-SHE-103) ──
    /// <summary>Records a submission to a regulatory body. Refused (422) unless the incident is
    /// flagged reportable; the first submission stamps the incident's notification fields.</summary>
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/statutory-submissions")]
    public async Task<ActionResult<SheStatutoryIncidentSubmissionDto>> AddStatutorySubmission(Guid id, [FromBody] CreateSheStatutoryIncidentSubmissionDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddStatutorySubmissionAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("{id:guid}/statutory-submissions")]
    public async Task<ActionResult<IEnumerable<SheStatutoryIncidentSubmissionDto>>> GetStatutorySubmissions(Guid id)
        => Ok(await _service.GetStatutorySubmissionsAsync(id));

    /// <summary>Records the authority's acknowledgement or corrects submission detail. No delete —
    /// statutory submissions are permanent records, like non-compliance notices.</summary>
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("statutory-submissions/{submissionId:guid}")]
    public async Task<ActionResult<SheStatutoryIncidentSubmissionDto>> UpdateStatutorySubmission(Guid submissionId, [FromBody] UpdateSheStatutoryIncidentSubmissionDto dto)
    {
        if (submissionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateStatutorySubmissionAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/file-claim")]
    public async Task<IActionResult> FileClaim(Guid id, [FromBody] FileSafetyIncidentClaimDto dto)
    {
        dto.IncidentId = id;
        await _service.FileClaimAsync(dto, UserId);
        return Ok(new { message = "Insurance claim recorded." });
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewSafetyIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.ReviewAsync(dto, UserId);
        return Ok(new { message = "Incident reviewed." });
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseSafetyIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.CloseAsync(dto, UserId);
        return Ok(new { message = "Incident closed." });
    }

    // ── Involved persons ──
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/involved-persons")]
    public async Task<ActionResult<SafetyIncidentInvolvedPersonDto>> AddInvolvedPerson(Guid id, [FromBody] CreateSafetyIncidentInvolvedPersonDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddInvolvedPersonAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("involved-persons/{personId:guid}")]
    public async Task<ActionResult<SafetyIncidentInvolvedPersonDto>> UpdateInvolvedPerson(Guid personId, [FromBody] UpdateSafetyIncidentInvolvedPersonDto dto)
    {
        if (personId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInvolvedPersonAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("involved-persons/{personId:guid}")]
    public async Task<IActionResult> DeleteInvolvedPerson(Guid personId)
    {
        await _service.DeleteInvolvedPersonAsync(personId);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("involved-persons/{personId:guid}/body-parts")]
    public async Task<ActionResult<SafetyIncidentInjuredBodyPartDto>> AddInjuredBodyPart(Guid personId, [FromBody] CreateSafetyIncidentInjuredBodyPartDto dto)
    {
        dto.InvolvedPersonId = personId;
        return Ok(await _service.AddInjuredBodyPartAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("body-parts/{bodyPartId:guid}")]
    public async Task<IActionResult> DeleteInjuredBodyPart(Guid bodyPartId)
    {
        await _service.DeleteInjuredBodyPartAsync(bodyPartId);
        return NoContent();
    }

    // ── Witnesses ──
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/witnesses")]
    public async Task<ActionResult<SafetyIncidentWitnessDto>> AddWitness(Guid id, [FromBody] CreateSafetyIncidentWitnessDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddWitnessAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("witnesses/{witnessId:guid}")]
    public async Task<ActionResult<SafetyIncidentWitnessDto>> UpdateWitness(Guid witnessId, [FromBody] UpdateSafetyIncidentWitnessDto dto)
    {
        if (witnessId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateWitnessAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("witnesses/{witnessId:guid}")]
    public async Task<IActionResult> DeleteWitness(Guid witnessId)
    {
        await _service.DeleteWitnessAsync(witnessId);
        return NoContent();
    }

    // ── Investigation team ──
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/investigation-team")]
    public async Task<ActionResult<SafetyIncidentInvestigationTeamMemberDto>> AddInvestigationTeamMember(Guid id, [FromBody] CreateSafetyIncidentInvestigationTeamMemberDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddInvestigationTeamMemberAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("investigation-team/{memberId:guid}")]
    public async Task<IActionResult> RemoveInvestigationTeamMember(Guid memberId)
    {
        await _service.RemoveInvestigationTeamMemberAsync(memberId);
        return NoContent();
    }

    // ── Corrective actions ──
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("{id:guid}/corrective-actions")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetCorrectiveActions(Guid id)
        => Ok(await _service.GetCorrectiveActionsForIncidentAsync(id));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("corrective-actions/overdue")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetOverdueCorrectiveActions()
        => Ok(await _service.GetOverdueCorrectiveActionsAsync());

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("corrective-actions/by-responsible/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetCorrectiveActionsByResponsible(Guid employeeId)
        => Ok(await _service.GetCorrectiveActionsByResponsibleAsync(employeeId));

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/corrective-actions")]
    public async Task<ActionResult<SafetyIncidentCorrectiveActionDto>> AddCorrectiveAction(Guid id, [FromBody] CreateSafetyIncidentCorrectiveActionDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddCorrectiveActionAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("corrective-actions/{actionId:guid}")]
    public async Task<ActionResult<SafetyIncidentCorrectiveActionDto>> UpdateCorrectiveAction(Guid actionId, [FromBody] UpdateSafetyIncidentCorrectiveActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateCorrectiveActionAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("corrective-actions/{actionId:guid}/verify")]
    public async Task<IActionResult> VerifyCorrectiveAction(Guid actionId, [FromBody] VerifySafetyIncidentCorrectiveActionDto dto)
    {
        dto.CorrectiveActionId = actionId;
        await _service.VerifyCorrectiveActionAsync(dto, UserId);
        return Ok(new { message = "Corrective action verified." });
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("corrective-actions/{actionId:guid}")]
    public async Task<IActionResult> DeleteCorrectiveAction(Guid actionId)
    {
        await _service.DeleteCorrectiveActionAsync(actionId);
        return NoContent();
    }

    // ── Follow-ups & documents ──
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/follow-ups")]
    public async Task<ActionResult<SafetyIncidentFollowUpDto>> AddFollowUp(Guid id, [FromBody] CreateSafetyIncidentFollowUpDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddFollowUpAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<SafetyIncidentDocumentDto>> AddDocument(Guid id, [FromBody] CreateSafetyIncidentDocumentDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddDocumentAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}
