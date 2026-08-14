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
/// corrective actions — is HR-gated. Employee self-service reads ("incidents I reported / am
/// involved in") are deliberately deferred to the employee-portal work (area 25), not an
/// oversight here.
/// </summary>
[ApiController]
[SafetyBusinessRules]
[Route("api/safety/incidents")]
[Authorize]
public class SafetyIncidentController : SheApiControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement could not be relaxed for the one report action employees need.
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly ISafetyIncidentService _service;

    public SafetyIncidentController(ISafetyIncidentService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Queries ──
    [Authorize(Roles = HrRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<SafetyIncidentSummaryDto>>> GetPaged(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] SheIncidentStatus? status = null)
        => Ok(await _service.GetPagedAsync(page, pageSize, status));

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyIncidentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("number/{incidentNumber}")]
    public async Task<ActionResult<SafetyIncidentDto?>> GetByNumber(string incidentNumber)
        => Ok(await _service.GetByNumberAsync(incidentNumber));

    [Authorize(Roles = HrRoles)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByStatus(SheIncidentStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [Authorize(Roles = HrRoles)]
    [HttpGet("severity/{severity}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetBySeverity(SheIncidentSeverity severity)
        => Ok(await _service.GetBySeverityAsync(severity));

    [Authorize(Roles = HrRoles)]
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByCategory(SheIncidentCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [Authorize(Roles = HrRoles)]
    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetByDateRangeAsync(from, to));

    [Authorize(Roles = HrRoles)]
    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByInvolvedEmployee(Guid employeeId)
        => Ok(await _service.GetByInvolvedEmployeeAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("for-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetForEmployee(Guid employeeId)
        => Ok(await _service.GetForEmployeeAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("requiring-investigation")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetRequiringInvestigation()
        => Ok(await _service.GetRequiringInvestigationAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("open")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetOpen()
        => Ok(await _service.GetOpenAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("lost-time")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetLostTimeInjuries()
        => Ok(await _service.GetLostTimeInjuriesAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("reportable-pending")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetReportableNotYetNotified()
        => Ok(await _service.GetReportableNotYetNotifiedAsync());

    // ── CRUD ──
    /// <summary>Open to any authenticated employee — incident reporting must not be gatekept. Non-HR
    /// reporters always report as themselves: the reporter comes from the token, never the body.</summary>
    [HttpPost]
    public async Task<ActionResult<SafetyIncidentDto>> Create([FromBody] CreateSafetyIncidentDto dto)
    {
        var isHr = User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);
        if (!isHr || dto.ReportedById == Guid.Empty)
            dto.ReportedById = UserId;
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetyIncidentDto>> Update(Guid id, [FromBody] UpdateSafetyIncidentDto dto)
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

    // ── Workflow ──
    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/assign-investigation")]
    public async Task<IActionResult> AssignInvestigation(Guid id, [FromBody] AssignSafetyIncidentInvestigationDto dto)
    {
        dto.IncidentId = id;
        await _service.AssignInvestigationAsync(dto, UserId);
        return Ok(new { message = "Investigation assigned." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/record-investigation")]
    public async Task<IActionResult> RecordInvestigation(Guid id, [FromBody] RecordSafetyIncidentInvestigationDto dto)
    {
        dto.IncidentId = id;
        await _service.RecordInvestigationAsync(dto, UserId);
        return Ok(new { message = "Investigation findings recorded." });
    }

    [Authorize(Roles = HrRoles)]
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
    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/statutory-submissions")]
    public async Task<ActionResult<SheStatutoryIncidentSubmissionDto>> AddStatutorySubmission(Guid id, [FromBody] CreateSheStatutoryIncidentSubmissionDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddStatutorySubmissionAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/statutory-submissions")]
    public async Task<ActionResult<IEnumerable<SheStatutoryIncidentSubmissionDto>>> GetStatutorySubmissions(Guid id)
        => Ok(await _service.GetStatutorySubmissionsAsync(id));

    /// <summary>Records the authority's acknowledgement or corrects submission detail. No delete —
    /// statutory submissions are permanent records, like non-compliance notices.</summary>
    [Authorize(Roles = HrRoles)]
    [HttpPut("statutory-submissions/{submissionId:guid}")]
    public async Task<ActionResult<SheStatutoryIncidentSubmissionDto>> UpdateStatutorySubmission(Guid submissionId, [FromBody] UpdateSheStatutoryIncidentSubmissionDto dto)
    {
        if (submissionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateStatutorySubmissionAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/file-claim")]
    public async Task<IActionResult> FileClaim(Guid id, [FromBody] FileSafetyIncidentClaimDto dto)
    {
        dto.IncidentId = id;
        await _service.FileClaimAsync(dto, UserId);
        return Ok(new { message = "Insurance claim recorded." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewSafetyIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.ReviewAsync(dto, UserId);
        return Ok(new { message = "Incident reviewed." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseSafetyIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.CloseAsync(dto, UserId);
        return Ok(new { message = "Incident closed." });
    }

    // ── Involved persons ──
    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/involved-persons")]
    public async Task<ActionResult<SafetyIncidentInvolvedPersonDto>> AddInvolvedPerson(Guid id, [FromBody] CreateSafetyIncidentInvolvedPersonDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddInvolvedPersonAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("involved-persons/{personId:guid}")]
    public async Task<ActionResult<SafetyIncidentInvolvedPersonDto>> UpdateInvolvedPerson(Guid personId, [FromBody] UpdateSafetyIncidentInvolvedPersonDto dto)
    {
        if (personId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInvolvedPersonAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("involved-persons/{personId:guid}")]
    public async Task<IActionResult> DeleteInvolvedPerson(Guid personId)
    {
        await _service.DeleteInvolvedPersonAsync(personId);
        return NoContent();
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("involved-persons/{personId:guid}/body-parts")]
    public async Task<ActionResult<SafetyIncidentInjuredBodyPartDto>> AddInjuredBodyPart(Guid personId, [FromBody] CreateSafetyIncidentInjuredBodyPartDto dto)
    {
        dto.InvolvedPersonId = personId;
        return Ok(await _service.AddInjuredBodyPartAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("body-parts/{bodyPartId:guid}")]
    public async Task<IActionResult> DeleteInjuredBodyPart(Guid bodyPartId)
    {
        await _service.DeleteInjuredBodyPartAsync(bodyPartId);
        return NoContent();
    }

    // ── Witnesses ──
    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/witnesses")]
    public async Task<ActionResult<SafetyIncidentWitnessDto>> AddWitness(Guid id, [FromBody] CreateSafetyIncidentWitnessDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddWitnessAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("witnesses/{witnessId:guid}")]
    public async Task<ActionResult<SafetyIncidentWitnessDto>> UpdateWitness(Guid witnessId, [FromBody] UpdateSafetyIncidentWitnessDto dto)
    {
        if (witnessId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateWitnessAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("witnesses/{witnessId:guid}")]
    public async Task<IActionResult> DeleteWitness(Guid witnessId)
    {
        await _service.DeleteWitnessAsync(witnessId);
        return NoContent();
    }

    // ── Investigation team ──
    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/investigation-team")]
    public async Task<ActionResult<SafetyIncidentInvestigationTeamMemberDto>> AddInvestigationTeamMember(Guid id, [FromBody] CreateSafetyIncidentInvestigationTeamMemberDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddInvestigationTeamMemberAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("investigation-team/{memberId:guid}")]
    public async Task<IActionResult> RemoveInvestigationTeamMember(Guid memberId)
    {
        await _service.RemoveInvestigationTeamMemberAsync(memberId);
        return NoContent();
    }

    // ── Corrective actions ──
    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/corrective-actions")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetCorrectiveActions(Guid id)
        => Ok(await _service.GetCorrectiveActionsForIncidentAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("corrective-actions/overdue")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetOverdueCorrectiveActions()
        => Ok(await _service.GetOverdueCorrectiveActionsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("corrective-actions/by-responsible/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetCorrectiveActionsByResponsible(Guid employeeId)
        => Ok(await _service.GetCorrectiveActionsByResponsibleAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/corrective-actions")]
    public async Task<ActionResult<SafetyIncidentCorrectiveActionDto>> AddCorrectiveAction(Guid id, [FromBody] CreateSafetyIncidentCorrectiveActionDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddCorrectiveActionAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("corrective-actions/{actionId:guid}")]
    public async Task<ActionResult<SafetyIncidentCorrectiveActionDto>> UpdateCorrectiveAction(Guid actionId, [FromBody] UpdateSafetyIncidentCorrectiveActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateCorrectiveActionAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("corrective-actions/{actionId:guid}/verify")]
    public async Task<IActionResult> VerifyCorrectiveAction(Guid actionId, [FromBody] VerifySafetyIncidentCorrectiveActionDto dto)
    {
        dto.CorrectiveActionId = actionId;
        await _service.VerifyCorrectiveActionAsync(dto, UserId);
        return Ok(new { message = "Corrective action verified." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("corrective-actions/{actionId:guid}")]
    public async Task<IActionResult> DeleteCorrectiveAction(Guid actionId)
    {
        await _service.DeleteCorrectiveActionAsync(actionId);
        return NoContent();
    }

    // ── Follow-ups & documents ──
    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/follow-ups")]
    public async Task<ActionResult<SafetyIncidentFollowUpDto>> AddFollowUp(Guid id, [FromBody] CreateSafetyIncidentFollowUpDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddFollowUpAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<SafetyIncidentDocumentDto>> AddDocument(Guid id, [FromBody] CreateSafetyIncidentDocumentDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddDocumentAsync(dto, TenantId, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}
