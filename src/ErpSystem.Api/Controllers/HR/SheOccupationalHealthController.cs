using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

// Gated on the Medical permission policies rather than the SHE HR-role gate: surveillance rows
// carry examination results and work restrictions — medical-grade data, per the agreed
// SHE↔Medical boundary. Reads need MedicalRead; writes MedicalWrite; deletes MedicalAdmin.
// HrPermissionRoleFallbackAuthorizationHandler keeps HR-role users working until the
// permission seed propagates.
[ApiController]
[Route("api/safety/occupational-health")]
[SafetyBusinessRules]
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class SheOccupationalHealthController : SheApiControllerBase
{
    private readonly ISheOccupationalHealthService _service;

    public SheOccupationalHealthController(ISheOccupationalHealthService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Health surveillance ──
    [HttpGet("surveillance")]
    public async Task<ActionResult<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>>> GetAllSurveillance()
        => Ok(await _service.GetAllSurveillanceAsync());

    [HttpGet("surveillance/{id:guid}")]
    public async Task<ActionResult<SheOccupationalHealthSurveillanceDto>> GetSurveillance(Guid id)
        => Ok(await _service.GetSurveillanceAsync(id));

    [HttpGet("surveillance/by-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>>> GetSurveillanceByEmployee(Guid employeeId)
        => Ok(await _service.GetSurveillanceByEmployeeAsync(employeeId));

    [HttpGet("surveillance/type/{type}")]
    public async Task<ActionResult<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>>> GetSurveillanceByType(SheHealthSurveillanceType type)
        => Ok(await _service.GetSurveillanceByTypeAsync(type));

    [HttpGet("surveillance/result/{result}")]
    public async Task<ActionResult<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>>> GetSurveillanceByResult(SheHealthSurveillanceResult result)
        => Ok(await _service.GetSurveillanceByResultAsync(result));

    [HttpGet("surveillance/due-for-examination")]
    public async Task<ActionResult<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>>> GetSurveillanceDueForExamination([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetSurveillanceDueForExaminationAsync(daysAhead));

    [HttpGet("surveillance/with-restrictions")]
    public async Task<ActionResult<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>>> GetSurveillanceWithRestrictions()
        => Ok(await _service.GetSurveillanceWithRestrictionsAsync());

    [HttpPost("surveillance")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheOccupationalHealthSurveillanceDto>> CreateSurveillance([FromBody] CreateSheOccupationalHealthSurveillanceDto dto)
    {
        var created = await _service.CreateSurveillanceAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetSurveillance), new { id = created.Id }, created);
    }

    [HttpPut("surveillance/{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheOccupationalHealthSurveillanceDto>> UpdateSurveillance(Guid id, [FromBody] UpdateSheOccupationalHealthSurveillanceDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateSurveillanceAsync(dto, UserId));
    }

    [HttpDelete("surveillance/{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    public async Task<IActionResult> DeleteSurveillance(Guid id)
    {
        await _service.DeleteSurveillanceAsync(id);
        return NoContent();
    }

    // ── First aid stations ──
    [HttpGet("first-aid-stations/{id:guid}")]
    public async Task<ActionResult<SheFirstAidStationDto>> GetFirstAidStation(Guid id)
        => Ok(await _service.GetFirstAidStationAsync(id));

    [HttpGet("first-aid-stations")]
    public async Task<ActionResult<IEnumerable<SheFirstAidStationDto>>> GetFirstAidStations([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetFirstAidStationsAsync(activeOnly));

    [HttpGet("first-aid-stations/by-location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SheFirstAidStationDto>>> GetFirstAidStationsByLocation(Guid locationId)
        => Ok(await _service.GetFirstAidStationsByLocationAsync(locationId));

    [HttpGet("first-aid-stations/due-for-inspection")]
    public async Task<ActionResult<IEnumerable<SheFirstAidStationDto>>> GetFirstAidStationsDueForInspection([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetFirstAidStationsDueForInspectionAsync(daysAhead));

    [HttpGet("first-aid-stations/under-stocked")]
    public async Task<ActionResult<IEnumerable<SheFirstAidStationDto>>> GetUnderStockedStations()
        => Ok(await _service.GetUnderStockedStationsAsync());

    [HttpPost("first-aid-stations")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheFirstAidStationDto>> CreateFirstAidStation([FromBody] CreateSheFirstAidStationDto dto)
    {
        var created = await _service.CreateFirstAidStationAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetFirstAidStation), new { id = created.Id }, created);
    }

    [HttpPut("first-aid-stations/{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheFirstAidStationDto>> UpdateFirstAidStation(Guid id, [FromBody] UpdateSheFirstAidStationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateFirstAidStationAsync(dto, UserId));
    }

    [HttpDelete("first-aid-stations/{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    public async Task<IActionResult> DeleteFirstAidStation(Guid id)
    {
        await _service.DeleteFirstAidStationAsync(id);
        return NoContent();
    }

    // ── Wellness programs ──
    [HttpGet("wellness-programs/{id:guid}")]
    public async Task<ActionResult<SheWellnessProgramDto>> GetWellnessProgram(Guid id)
        => Ok(await _service.GetWellnessProgramAsync(id));

    [HttpGet("wellness-programs")]
    public async Task<ActionResult<IEnumerable<SheWellnessProgramDto>>> GetWellnessPrograms([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetWellnessProgramsAsync(activeOnly));

    [HttpGet("wellness-programs/status/{status}")]
    public async Task<ActionResult<IEnumerable<SheWellnessProgramDto>>> GetWellnessProgramsByStatus(SheWellnessProgramStatus status)
        => Ok(await _service.GetWellnessProgramsByStatusAsync(status));

    [HttpGet("wellness-programs/type/{type}")]
    public async Task<ActionResult<IEnumerable<SheWellnessProgramDto>>> GetWellnessProgramsByType(SheWellnessProgramType type)
        => Ok(await _service.GetWellnessProgramsByTypeAsync(type));

    [HttpPost("wellness-programs")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheWellnessProgramDto>> CreateWellnessProgram([FromBody] CreateSheWellnessProgramDto dto)
    {
        var created = await _service.CreateWellnessProgramAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetWellnessProgram), new { id = created.Id }, created);
    }

    [HttpPut("wellness-programs/{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<SheWellnessProgramDto>> UpdateWellnessProgram(Guid id, [FromBody] UpdateSheWellnessProgramDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateWellnessProgramAsync(dto, UserId));
    }

    [HttpDelete("wellness-programs/{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    public async Task<IActionResult> DeleteWellnessProgram(Guid id)
    {
        await _service.DeleteWellnessProgramAsync(id);
        return NoContent();
    }
}
