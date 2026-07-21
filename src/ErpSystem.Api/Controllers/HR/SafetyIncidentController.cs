using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/incidents")]
[Authorize]
public class SafetyIncidentController : SheApiControllerBase
{
    private readonly ISafetyIncidentService _service;

    public SafetyIncidentController(ISafetyIncidentService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Queries ──
    [HttpGet]
    public async Task<ActionResult<PagedResult<SafetyIncidentSummaryDto>>> GetPaged(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] SheIncidentStatus? status = null)
        => Ok(await _service.GetPagedAsync(page, pageSize, status));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyIncidentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{incidentNumber}")]
    public async Task<ActionResult<SafetyIncidentDto?>> GetByNumber(string incidentNumber)
        => Ok(await _service.GetByNumberAsync(incidentNumber));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByStatus(SheIncidentStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("severity/{severity}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetBySeverity(SheIncidentSeverity severity)
        => Ok(await _service.GetBySeverityAsync(severity));

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByCategory(SheIncidentCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetByDateRangeAsync(from, to));

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetByInvolvedEmployee(Guid employeeId)
        => Ok(await _service.GetByInvolvedEmployeeAsync(employeeId));

    [HttpGet("for-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetForEmployee(Guid employeeId)
        => Ok(await _service.GetForEmployeeAsync(employeeId));

    [HttpGet("requiring-investigation")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetRequiringInvestigation()
        => Ok(await _service.GetRequiringInvestigationAsync());

    [HttpGet("open")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetOpen()
        => Ok(await _service.GetOpenAsync());

    [HttpGet("lost-time")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetLostTimeInjuries()
        => Ok(await _service.GetLostTimeInjuriesAsync());

    [HttpGet("reportable-pending")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentSummaryDto>>> GetReportableNotYetNotified()
        => Ok(await _service.GetReportableNotYetNotifiedAsync());

    // ── CRUD ──
    [HttpPost]
    public async Task<ActionResult<SafetyIncidentDto>> Create([FromBody] CreateSafetyIncidentDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetyIncidentDto>> Update(Guid id, [FromBody] UpdateSafetyIncidentDto dto)
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

    // ── Workflow ──
    [HttpPost("{id:guid}/assign-investigation")]
    public async Task<IActionResult> AssignInvestigation(Guid id, [FromBody] AssignSafetyIncidentInvestigationDto dto)
    {
        dto.IncidentId = id;
        await _service.AssignInvestigationAsync(dto, UserId);
        return Ok(new { message = "Investigation assigned." });
    }

    [HttpPost("{id:guid}/record-investigation")]
    public async Task<IActionResult> RecordInvestigation(Guid id, [FromBody] RecordSafetyIncidentInvestigationDto dto)
    {
        dto.IncidentId = id;
        await _service.RecordInvestigationAsync(dto, UserId);
        return Ok(new { message = "Investigation findings recorded." });
    }

    [HttpPost("{id:guid}/notify-authority")]
    public async Task<IActionResult> NotifyAuthority(Guid id, [FromBody] NotifySafetyIncidentAuthorityDto dto)
    {
        dto.IncidentId = id;
        await _service.NotifyAuthorityAsync(dto, UserId);
        return Ok(new { message = "Authority notification recorded." });
    }

    [HttpPost("{id:guid}/file-claim")]
    public async Task<IActionResult> FileClaim(Guid id, [FromBody] FileSafetyIncidentClaimDto dto)
    {
        dto.IncidentId = id;
        await _service.FileClaimAsync(dto, UserId);
        return Ok(new { message = "Insurance claim recorded." });
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewSafetyIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.ReviewAsync(dto, UserId);
        return Ok(new { message = "Incident reviewed." });
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseSafetyIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.CloseAsync(dto, UserId);
        return Ok(new { message = "Incident closed." });
    }

    // ── Involved persons ──
    [HttpPost("{id:guid}/involved-persons")]
    public async Task<ActionResult<SafetyIncidentInvolvedPersonDto>> AddInvolvedPerson(Guid id, [FromBody] CreateSafetyIncidentInvolvedPersonDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddInvolvedPersonAsync(dto, TenantId, UserId));
    }

    [HttpPut("involved-persons/{personId:guid}")]
    public async Task<ActionResult<SafetyIncidentInvolvedPersonDto>> UpdateInvolvedPerson(Guid personId, [FromBody] UpdateSafetyIncidentInvolvedPersonDto dto)
    {
        if (personId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInvolvedPersonAsync(dto, UserId));
    }

    [HttpDelete("involved-persons/{personId:guid}")]
    public async Task<IActionResult> DeleteInvolvedPerson(Guid personId)
    {
        await _service.DeleteInvolvedPersonAsync(personId);
        return NoContent();
    }

    [HttpPost("involved-persons/{personId:guid}/body-parts")]
    public async Task<ActionResult<SafetyIncidentInjuredBodyPartDto>> AddInjuredBodyPart(Guid personId, [FromBody] CreateSafetyIncidentInjuredBodyPartDto dto)
    {
        dto.InvolvedPersonId = personId;
        return Ok(await _service.AddInjuredBodyPartAsync(dto, TenantId, UserId));
    }

    [HttpDelete("body-parts/{bodyPartId:guid}")]
    public async Task<IActionResult> DeleteInjuredBodyPart(Guid bodyPartId)
    {
        await _service.DeleteInjuredBodyPartAsync(bodyPartId);
        return NoContent();
    }

    // ── Witnesses ──
    [HttpPost("{id:guid}/witnesses")]
    public async Task<ActionResult<SafetyIncidentWitnessDto>> AddWitness(Guid id, [FromBody] CreateSafetyIncidentWitnessDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddWitnessAsync(dto, TenantId, UserId));
    }

    [HttpPut("witnesses/{witnessId:guid}")]
    public async Task<ActionResult<SafetyIncidentWitnessDto>> UpdateWitness(Guid witnessId, [FromBody] UpdateSafetyIncidentWitnessDto dto)
    {
        if (witnessId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateWitnessAsync(dto, UserId));
    }

    [HttpDelete("witnesses/{witnessId:guid}")]
    public async Task<IActionResult> DeleteWitness(Guid witnessId)
    {
        await _service.DeleteWitnessAsync(witnessId);
        return NoContent();
    }

    // ── Investigation team ──
    [HttpPost("{id:guid}/investigation-team")]
    public async Task<ActionResult<SafetyIncidentInvestigationTeamMemberDto>> AddInvestigationTeamMember(Guid id, [FromBody] CreateSafetyIncidentInvestigationTeamMemberDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddInvestigationTeamMemberAsync(dto, TenantId, UserId));
    }

    [HttpDelete("investigation-team/{memberId:guid}")]
    public async Task<IActionResult> RemoveInvestigationTeamMember(Guid memberId)
    {
        await _service.RemoveInvestigationTeamMemberAsync(memberId);
        return NoContent();
    }

    // ── Corrective actions ──
    [HttpGet("{id:guid}/corrective-actions")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetCorrectiveActions(Guid id)
        => Ok(await _service.GetCorrectiveActionsForIncidentAsync(id));

    [HttpGet("corrective-actions/overdue")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetOverdueCorrectiveActions()
        => Ok(await _service.GetOverdueCorrectiveActionsAsync());

    [HttpGet("corrective-actions/by-responsible/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyIncidentCorrectiveActionDto>>> GetCorrectiveActionsByResponsible(Guid employeeId)
        => Ok(await _service.GetCorrectiveActionsByResponsibleAsync(employeeId));

    [HttpPost("{id:guid}/corrective-actions")]
    public async Task<ActionResult<SafetyIncidentCorrectiveActionDto>> AddCorrectiveAction(Guid id, [FromBody] CreateSafetyIncidentCorrectiveActionDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddCorrectiveActionAsync(dto, TenantId, UserId));
    }

    [HttpPut("corrective-actions/{actionId:guid}")]
    public async Task<ActionResult<SafetyIncidentCorrectiveActionDto>> UpdateCorrectiveAction(Guid actionId, [FromBody] UpdateSafetyIncidentCorrectiveActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateCorrectiveActionAsync(dto, UserId));
    }

    [HttpPost("corrective-actions/{actionId:guid}/verify")]
    public async Task<IActionResult> VerifyCorrectiveAction(Guid actionId, [FromBody] VerifySafetyIncidentCorrectiveActionDto dto)
    {
        dto.CorrectiveActionId = actionId;
        await _service.VerifyCorrectiveActionAsync(dto, UserId);
        return Ok(new { message = "Corrective action verified." });
    }

    [HttpDelete("corrective-actions/{actionId:guid}")]
    public async Task<IActionResult> DeleteCorrectiveAction(Guid actionId)
    {
        await _service.DeleteCorrectiveActionAsync(actionId);
        return NoContent();
    }

    // ── Follow-ups & documents ──
    [HttpPost("{id:guid}/follow-ups")]
    public async Task<ActionResult<SafetyIncidentFollowUpDto>> AddFollowUp(Guid id, [FromBody] CreateSafetyIncidentFollowUpDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddFollowUpAsync(dto, TenantId, UserId));
    }

    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<SafetyIncidentDocumentDto>> AddDocument(Guid id, [FromBody] CreateSafetyIncidentDocumentDto dto)
    {
        dto.IncidentId = id;
        return Ok(await _service.AddDocumentAsync(dto, TenantId, UserId));
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}
