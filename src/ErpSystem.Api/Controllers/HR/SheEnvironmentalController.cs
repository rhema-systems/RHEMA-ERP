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
/// Environmental incidents + monitoring records. Since slice 17 this controller
/// carries the area's fifth deliberately-open employee action (FR-ENV-025): ANY
/// authenticated employee may report an environmental incident and read their
/// own reports — the reporter comes from the token, never the body, and the
/// creation auto-alerts the SHE audience. Investigation, closure, monitoring
/// and the registers stay HR-gated.
/// </summary>
[ApiController]
[Route("api/safety/environmental")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheEnvironmentalController : SheApiControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement could not be relaxed for the open report + mine actions.
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly ISheEnvironmentalService _service;

    public SheEnvironmentalController(ISheEnvironmentalService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── the open surface (FR-ENV-025) ──
    /// <summary>Open to any authenticated employee. Non-HR reporters report as themselves.</summary>
    [HttpPost("incidents")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto>> CreateIncident([FromBody] CreateSheEnvironmentalIncidentDto dto)
    {
        var isHr = User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);
        dto.ReportedById = isHr && dto.ReportedById != Guid.Empty ? dto.ReportedById : UserId;
        var created = await _service.CreateIncidentAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetIncident), new { id = created.Id }, created);
    }

    /// <summary>The caller's own reported incidents — open self-service (the slice-6 /mine idiom).</summary>
    [HttpGet("incidents/mine")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetMyIncidents()
        => Ok(await _service.GetMyIncidentsAsync(UserId));

    // ── Incidents (HR register) ──
    [Authorize(Roles = HrRoles)]
    [HttpGet("incidents/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto>> GetIncident(Guid id)
        => Ok(await _service.GetIncidentAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("incidents/number/{incidentNumber}")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto?>> GetIncidentByNumber(string incidentNumber)
        => Ok(await _service.GetIncidentByNumberAsync(incidentNumber));

    [Authorize(Roles = HrRoles)]
    [HttpGet("incidents/status/{status}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsByStatus(SheEnvironmentalIncidentStatus status)
        => Ok(await _service.GetIncidentsByStatusAsync(status));

    [Authorize(Roles = HrRoles)]
    [HttpGet("incidents/type/{type}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsByType(SheEnvironmentalIncidentType type)
        => Ok(await _service.GetIncidentsByTypeAsync(type));

    [Authorize(Roles = HrRoles)]
    [HttpGet("incidents/date-range")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetIncidentsByDateRangeAsync(from, to));

    [Authorize(Roles = HrRoles)]
    [HttpGet("incidents/open")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetOpenIncidents()
        => Ok(await _service.GetOpenIncidentsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("incidents/reported-to-epa")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalIncidentSummaryDto>>> GetIncidentsReportedToEpa()
        => Ok(await _service.GetIncidentsReportedToEpaAsync());

    [Authorize(Roles = HrRoles)]
    [HttpPut("incidents/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalIncidentDto>> UpdateIncident(Guid id, [FromBody] UpdateSheEnvironmentalIncidentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateIncidentAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("incidents/{id:guid}/close")]
    public async Task<IActionResult> CloseIncident(Guid id, [FromBody] CloseSheEnvironmentalIncidentDto dto)
    {
        dto.IncidentId = id;
        await _service.CloseIncidentAsync(dto, UserId);
        return Ok(new { message = "Environmental incident closed." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("incidents/{id:guid}")]
    public async Task<IActionResult> DeleteIncident(Guid id)
    {
        await _service.DeleteIncidentAsync(id);
        return NoContent();
    }

    // ── Monitoring ──
    [Authorize(Roles = HrRoles)]
    [HttpGet("monitoring/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalMonitoringRecordDto>> GetMonitoringRecord(Guid id)
        => Ok(await _service.GetMonitoringRecordAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("monitoring/type/{type}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetMonitoringByType(SheEnvironmentalMonitoringType type)
        => Ok(await _service.GetMonitoringByTypeAsync(type));

    [Authorize(Roles = HrRoles)]
    [HttpGet("monitoring/date-range")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetMonitoringByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetMonitoringByDateRangeAsync(from, to));

    [Authorize(Roles = HrRoles)]
    [HttpGet("monitoring/location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetMonitoringByLocation(Guid locationId)
        => Ok(await _service.GetMonitoringByLocationAsync(locationId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("monitoring/exceedances")]
    public async Task<ActionResult<IEnumerable<SheEnvironmentalMonitoringRecordDto>>> GetExceedances()
        => Ok(await _service.GetExceedancesAsync());

    [Authorize(Roles = HrRoles)]
    [HttpPost("monitoring")]
    public async Task<ActionResult<SheEnvironmentalMonitoringRecordDto>> CreateMonitoringRecord([FromBody] CreateSheEnvironmentalMonitoringRecordDto dto)
    {
        var created = await _service.CreateMonitoringRecordAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetMonitoringRecord), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("monitoring/{id:guid}")]
    public async Task<ActionResult<SheEnvironmentalMonitoringRecordDto>> UpdateMonitoringRecord(Guid id, [FromBody] UpdateSheEnvironmentalMonitoringRecordDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateMonitoringRecordAsync(dto, UserId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("monitoring/{id:guid}")]
    public async Task<IActionResult> DeleteMonitoringRecord(Guid id)
    {
        await _service.DeleteMonitoringRecordAsync(id);
        return NoContent();
    }
}
