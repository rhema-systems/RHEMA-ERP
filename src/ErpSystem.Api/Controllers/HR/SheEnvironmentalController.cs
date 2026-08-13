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
[Route("api/safety/environmental")]
[SafetyBusinessRules]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class SheEnvironmentalController : SheApiControllerBase
{
    private readonly ISheEnvironmentalService _service;

    public SheEnvironmentalController(ISheEnvironmentalService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Incidents ──
    [HttpGet("incidents/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto>> GetIncident(Guid id)
        => Ok(await _service.GetIncidentAsync(id));

    [HttpGet("incidents/number/{incidentNumber}")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto?>> GetIncidentByNumber(string incidentNumber)
        => Ok(await _service.GetIncidentByNumberAsync(incidentNumber));

    [HttpGet("incidents/status/{status}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsByStatus(SheEnvironmentalIncidentStatus status)
        => Ok(await _service.GetIncidentsByStatusAsync(status));

    [HttpGet("incidents/type/{type}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsByType(SheEnvironmentalIncidentType type)
        => Ok(await _service.GetIncidentsByTypeAsync(type));

    [HttpGet("incidents/date-range")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetIncidentsByDateRangeAsync(from, to));

    [HttpGet("incidents/open")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetOpenIncidents()
        => Ok(await _service.GetOpenIncidentsAsync());

    [HttpGet("incidents/reported-to-epa")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsReportedToEpa()
        => Ok(await _service.GetIncidentsReportedToEpaAsync());

    [HttpPost("incidents")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto>> CreateIncident([FromBody] CreateSheEnvironmentalIncidentDto dto)
    {
        var created = await _service.CreateIncidentAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetIncident), new { id = created.Id }, created);
    }

    [HttpPut("incidents/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto>> UpdateIncident(Guid id, [FromBody] UpdateSheEnvironmentalIncidentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateIncidentAsync(dto, UserId));
    }

    [HttpPost("incidents/{id:guid}/close")]
    public async Task<IActionResult> CloseIncident(Guid id, [FromBody] CloseSheEnvironmentalIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.CloseIncidentAsync(dto, UserId);
        return Ok(new { message = "Environmental incident closed." });
    }

    [HttpDelete("incidents/{id:guid}")]
    public async Task<IActionResult> DeleteIncident(Guid id)
    {
        await _service.DeleteIncidentAsync(id);
        return NoContent();
    }

    // ── Monitoring ──
    [HttpGet("monitoring/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalMonitoringRecordDto>> GetMonitoringRecord(Guid id)
        => Ok(await _service.GetMonitoringRecordAsync(id));

    [HttpGet("monitoring/type/{type}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetMonitoringByType(SheEnvironmentalMonitoringType type)
        => Ok(await _service.GetMonitoringByTypeAsync(type));

    [HttpGet("monitoring/date-range")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetMonitoringByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetMonitoringByDateRangeAsync(from, to));

    [HttpGet("monitoring/location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetMonitoringByLocation(Guid locationId)
        => Ok(await _service.GetMonitoringByLocationAsync(locationId));

    [HttpGet("monitoring/exceedances")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetExceedances()
        => Ok(await _service.GetExceedancesAsync());

    [HttpPost("monitoring")]
    public async Task<ActionResult<SheEnvironmentalMonitoringRecordDto>> CreateMonitoringRecord([FromBody] CreateSheEnvironmentalMonitoringRecordDto dto)
    {
        var created = await _service.CreateMonitoringRecordAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetMonitoringRecord), new { id = created.Id }, created);
    }

    [HttpPut("monitoring/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalMonitoringRecordDto>> UpdateMonitoringRecord(Guid id, [FromBody] UpdateSheEnvironmentalMonitoringRecordDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateMonitoringRecordAsync(dto, UserId));
    }

    [HttpDelete("monitoring/{id:guid}")]
    public async Task<IActionResult> DeleteMonitoringRecord(Guid id)
    {
        await _service.DeleteMonitoringRecordAsync(id);
        return NoContent();
    }
}
