using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/reference")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheReferenceDataController : SheApiControllerBase
{
    private readonly ISheReferenceDataService _service;

    public SheReferenceDataController(ISheReferenceDataService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Incident types ──
    // W3 slice 12: the two LIST reads stay open to internal users — the employee report-incident
    // form fills its type picker from them (the leave-type precedent: reference reads a
    // self-service form feeds on are not desk data). Under the previous SuperAdmin/HR class gate
    // the picker silently 403'd and every employee-filed incident arrived unclassified.
    [HttpGet("incident-types")]
    public async Task<ActionResult<IEnumerable<SheIncidentTypeDto>>> GetIncidentTypes([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetIncidentTypesAsync(activeOnly));

    [HttpGet("incident-types/reportable")]
    public async Task<ActionResult<IEnumerable<SheIncidentTypeDto>>> GetReportableIncidentTypes()
        => Ok(await _service.GetReportableIncidentTypesAsync());

    [HttpGet("incident-types/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheIncidentTypeDto>> GetIncidentType(Guid id)
        => Ok(await _service.GetIncidentTypeAsync(id));

    [HttpPost("incident-types")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheIncidentTypeDto>> CreateIncidentType([FromBody] CreateSheIncidentTypeDto dto)
        => Ok(await _service.CreateIncidentTypeAsync(dto, TenantId, UserId));

    [HttpPut("incident-types/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheIncidentTypeDto>> UpdateIncidentType(Guid id, [FromBody] UpdateSheIncidentTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateIncidentTypeAsync(dto, UserId));
    }

    [HttpDelete("incident-types/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteIncidentType(Guid id)
    {
        await _service.DeleteIncidentTypeAsync(id);
        return NoContent();
    }

    // ── Default corrective actions on an incident type (auto-populate onto new incidents) ──
    [HttpPost("incident-types/{id:guid}/corrective-actions")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheIncidentTypeCorrectiveActionDto>> AddIncidentTypeCorrectiveAction(Guid id, [FromBody] CreateSheIncidentTypeCorrectiveActionDto dto)
    {
        dto.IncidentTypeId = id;
        return Ok(await _service.AddIncidentTypeCorrectiveActionAsync(dto, TenantId, UserId));
    }

    [HttpPut("incident-types/corrective-actions/{linkId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheIncidentTypeCorrectiveActionDto>> UpdateIncidentTypeCorrectiveAction(Guid linkId, [FromBody] UpdateSheIncidentTypeCorrectiveActionDto dto)
    {
        if (linkId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateIncidentTypeCorrectiveActionAsync(dto, UserId));
    }

    [HttpDelete("incident-types/corrective-actions/{linkId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> RemoveIncidentTypeCorrectiveAction(Guid linkId)
    {
        await _service.RemoveIncidentTypeCorrectiveActionAsync(linkId);
        return NoContent();
    }

    // ── Injury types ──
    [HttpGet("injury-types")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheInjuryTypeDto>>> GetInjuryTypes([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetInjuryTypesAsync(activeOnly));

    [HttpPost("injury-types")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInjuryTypeDto>> CreateInjuryType([FromBody] CreateSheInjuryTypeDto dto)
        => Ok(await _service.CreateInjuryTypeAsync(dto, TenantId, UserId));

    [HttpPut("injury-types/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInjuryTypeDto>> UpdateInjuryType(Guid id, [FromBody] UpdateSheInjuryTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInjuryTypeAsync(dto, UserId));
    }

    [HttpDelete("injury-types/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteInjuryType(Guid id)
    {
        await _service.DeleteInjuryTypeAsync(id);
        return NoContent();
    }

    // ── Body parts ──
    [HttpGet("body-parts")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheBodyPartDto>>> GetBodyParts([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetBodyPartsAsync(activeOnly));

    [HttpPost("body-parts")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheBodyPartDto>> CreateBodyPart([FromBody] CreateSheBodyPartDto dto)
        => Ok(await _service.CreateBodyPartAsync(dto, TenantId, UserId));

    [HttpPut("body-parts/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheBodyPartDto>> UpdateBodyPart(Guid id, [FromBody] UpdateSheBodyPartDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateBodyPartAsync(dto, UserId));
    }

    [HttpDelete("body-parts/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteBodyPart(Guid id)
    {
        await _service.DeleteBodyPartAsync(id);
        return NoContent();
    }

    // ── Corrective action templates ──
    [HttpGet("corrective-action-templates")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheCorrectiveActionTemplateDto>>> GetCorrectiveActionTemplates([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetCorrectiveActionTemplatesAsync(activeOnly));

    [HttpPost("corrective-action-templates")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheCorrectiveActionTemplateDto>> CreateCorrectiveActionTemplate([FromBody] CreateSheCorrectiveActionTemplateDto dto)
        => Ok(await _service.CreateCorrectiveActionTemplateAsync(dto, TenantId, UserId));

    [HttpPut("corrective-action-templates/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheCorrectiveActionTemplateDto>> UpdateCorrectiveActionTemplate(Guid id, [FromBody] UpdateSheCorrectiveActionTemplateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateCorrectiveActionTemplateAsync(dto, UserId));
    }

    [HttpDelete("corrective-action-templates/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteCorrectiveActionTemplate(Guid id)
    {
        await _service.DeleteCorrectiveActionTemplateAsync(id);
        return NoContent();
    }

    // ── Regulatory bodies ──
    [HttpGet("regulatory-bodies")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheRegulatoryBodyDto>>> GetRegulatoryBodies([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetRegulatoryBodiesAsync(activeOnly));

    [HttpPost("regulatory-bodies")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheRegulatoryBodyDto>> CreateRegulatoryBody([FromBody] CreateSheRegulatoryBodyDto dto)
        => Ok(await _service.CreateRegulatoryBodyAsync(dto, TenantId, UserId));

    [HttpPut("regulatory-bodies/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheRegulatoryBodyDto>> UpdateRegulatoryBody(Guid id, [FromBody] UpdateSheRegulatoryBodyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateRegulatoryBodyAsync(dto, UserId));
    }

    [HttpDelete("regulatory-bodies/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteRegulatoryBody(Guid id)
    {
        await _service.DeleteRegulatoryBodyAsync(id);
        return NoContent();
    }
}
