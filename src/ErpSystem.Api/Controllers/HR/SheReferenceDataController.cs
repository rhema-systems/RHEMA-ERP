using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/reference")]
[Authorize]
public class SheReferenceDataController : SheApiControllerBase
{
    private readonly ISheReferenceDataService _service;

    public SheReferenceDataController(ISheReferenceDataService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Incident types ──
    [HttpGet("incident-types")]
    public async Task<ActionResult<IEnumerable<SheIncidentTypeDto>>> GetIncidentTypes([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetIncidentTypesAsync(activeOnly));

    [HttpGet("incident-types/reportable")]
    public async Task<ActionResult<IEnumerable<SheIncidentTypeDto>>> GetReportableIncidentTypes()
        => Ok(await _service.GetReportableIncidentTypesAsync());

    [HttpGet("incident-types/{id:guid}")]
    public async Task<ActionResult<SheIncidentTypeDto>> GetIncidentType(Guid id)
        => Ok(await _service.GetIncidentTypeAsync(id));

    [HttpPost("incident-types")]
    public async Task<ActionResult<SheIncidentTypeDto>> CreateIncidentType([FromBody] CreateSheIncidentTypeDto dto)
        => Ok(await _service.CreateIncidentTypeAsync(dto, TenantId, UserId));

    [HttpPut("incident-types/{id:guid}")]
    public async Task<ActionResult<SheIncidentTypeDto>> UpdateIncidentType(Guid id, [FromBody] UpdateSheIncidentTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateIncidentTypeAsync(dto, UserId));
    }

    [HttpDelete("incident-types/{id:guid}")]
    public async Task<IActionResult> DeleteIncidentType(Guid id)
    {
        await _service.DeleteIncidentTypeAsync(id);
        return NoContent();
    }

    // ── Injury types ──
    [HttpGet("injury-types")]
    public async Task<ActionResult<IEnumerable<SheInjuryTypeDto>>> GetInjuryTypes([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetInjuryTypesAsync(activeOnly));

    [HttpPost("injury-types")]
    public async Task<ActionResult<SheInjuryTypeDto>> CreateInjuryType([FromBody] CreateSheInjuryTypeDto dto)
        => Ok(await _service.CreateInjuryTypeAsync(dto, TenantId, UserId));

    [HttpPut("injury-types/{id:guid}")]
    public async Task<ActionResult<SheInjuryTypeDto>> UpdateInjuryType(Guid id, [FromBody] UpdateSheInjuryTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInjuryTypeAsync(dto, UserId));
    }

    [HttpDelete("injury-types/{id:guid}")]
    public async Task<IActionResult> DeleteInjuryType(Guid id)
    {
        await _service.DeleteInjuryTypeAsync(id);
        return NoContent();
    }

    // ── Body parts ──
    [HttpGet("body-parts")]
    public async Task<ActionResult<IEnumerable<SheBodyPartDto>>> GetBodyParts([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetBodyPartsAsync(activeOnly));

    [HttpPost("body-parts")]
    public async Task<ActionResult<SheBodyPartDto>> CreateBodyPart([FromBody] CreateSheBodyPartDto dto)
        => Ok(await _service.CreateBodyPartAsync(dto, TenantId, UserId));

    [HttpPut("body-parts/{id:guid}")]
    public async Task<ActionResult<SheBodyPartDto>> UpdateBodyPart(Guid id, [FromBody] UpdateSheBodyPartDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateBodyPartAsync(dto, UserId));
    }

    [HttpDelete("body-parts/{id:guid}")]
    public async Task<IActionResult> DeleteBodyPart(Guid id)
    {
        await _service.DeleteBodyPartAsync(id);
        return NoContent();
    }

    // ── Corrective action templates ──
    [HttpGet("corrective-action-templates")]
    public async Task<ActionResult<IEnumerable<SheCorrectiveActionTemplateDto>>> GetCorrectiveActionTemplates([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetCorrectiveActionTemplatesAsync(activeOnly));

    [HttpPost("corrective-action-templates")]
    public async Task<ActionResult<SheCorrectiveActionTemplateDto>> CreateCorrectiveActionTemplate([FromBody] CreateSheCorrectiveActionTemplateDto dto)
        => Ok(await _service.CreateCorrectiveActionTemplateAsync(dto, TenantId, UserId));

    [HttpPut("corrective-action-templates/{id:guid}")]
    public async Task<ActionResult<SheCorrectiveActionTemplateDto>> UpdateCorrectiveActionTemplate(Guid id, [FromBody] UpdateSheCorrectiveActionTemplateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateCorrectiveActionTemplateAsync(dto, UserId));
    }

    [HttpDelete("corrective-action-templates/{id:guid}")]
    public async Task<IActionResult> DeleteCorrectiveActionTemplate(Guid id)
    {
        await _service.DeleteCorrectiveActionTemplateAsync(id);
        return NoContent();
    }

    // ── Regulatory bodies ──
    [HttpGet("regulatory-bodies")]
    public async Task<ActionResult<IEnumerable<SheRegulatoryBodyDto>>> GetRegulatoryBodies([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetRegulatoryBodiesAsync(activeOnly));

    [HttpPost("regulatory-bodies")]
    public async Task<ActionResult<SheRegulatoryBodyDto>> CreateRegulatoryBody([FromBody] CreateSheRegulatoryBodyDto dto)
        => Ok(await _service.CreateRegulatoryBodyAsync(dto, TenantId, UserId));

    [HttpPut("regulatory-bodies/{id:guid}")]
    public async Task<ActionResult<SheRegulatoryBodyDto>> UpdateRegulatoryBody(Guid id, [FromBody] UpdateSheRegulatoryBodyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateRegulatoryBodyAsync(dto, UserId));
    }

    [HttpDelete("regulatory-bodies/{id:guid}")]
    public async Task<IActionResult> DeleteRegulatoryBody(Guid id)
    {
        await _service.DeleteRegulatoryBodyAsync(id);
        return NoContent();
    }
}
