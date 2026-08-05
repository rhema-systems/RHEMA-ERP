using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Vacant-position tracking: the establishment overview (all positions, filled and vacant), the logged
/// vacancies, and the "raise a requisition from a vacancy" flow.
/// </summary>
[ApiController]
[Route("api/position-vacancies")]
[Authorize]
public class PositionVacanciesController : ControllerBase
{
    private readonly IPositionVacancyService _service;
    private readonly ICurrentUserService _currentUser;

    public PositionVacanciesController(IPositionVacancyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    /// <summary>All active positions with filled vs vacant headcount and any open vacancy.</summary>
    [HttpGet("establishment")]
    public async Task<ActionResult<IEnumerable<PositionEstablishmentDto>>> GetEstablishment(
        [FromQuery] Guid? organizationUnitId, [FromQuery] bool onlyVacant = false, CancellationToken ct = default)
        => Ok(await _service.GetEstablishmentOverviewAsync(organizationUnitId, onlyVacant, ct));

    /// <summary>Logged position vacancies (open by default; pass includeClosed=true for history).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PositionVacancySummaryDto>>> GetVacancies(
        [FromQuery] PositionVacancyStatus? status,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] VacancyReason? reason,
        [FromQuery] VacancyClassification? classification,
        [FromQuery] bool includeClosed = false,
        CancellationToken ct = default)
        => Ok(await _service.GetVacanciesAsync(status, organizationUnitId, reason, classification, includeClosed, ct));

    [HttpGet("stats")]
    public async Task<ActionResult<PositionVacancyStatsDto>> GetStats(CancellationToken ct)
        => Ok(await _service.GetStatsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PositionVacancyDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result == null ? NotFound(new { message = "Vacancy not found." }) : Ok(result);
    }

    // ── Mutations ───────────────────────────────────────────────────────────

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<PositionVacancyDto>> UpdateStatus(
        Guid id, [FromBody] UpdatePositionVacancyStatusDto dto, CancellationToken ct)
    {
        if (id != dto.VacancyId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        try
        {
            return Ok(await _service.UpdateStatusAsync(dto, employeeId, ct));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}/notes")]
    public async Task<ActionResult<PositionVacancyDto>> UpdateNotes(
        Guid id, [FromBody] UpdateNotesRequest request, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        try
        {
            return Ok(await _service.UpdateNotesAsync(id, request?.Notes, employeeId, ct));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<PositionVacancyDto>> Close(
        Guid id, [FromBody] ClosePositionVacancyDto dto, CancellationToken ct)
    {
        if (id != dto.VacancyId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;
        try
        {
            return Ok(await _service.CloseAsync(dto, employeeId, ct));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Raises a Draft requisition pre-filled from the vacancy and returns its id for editing.</summary>
    [HttpPost("{id:guid}/raise-requisition")]
    public async Task<ActionResult<RaiseRequisitionResultDto>> RaiseRequisition(
        Guid id, [FromBody] RaiseRequisitionFromVacancyDto? dto, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest(new { message = "Tenant context could not be resolved." });
        if (employeeId == null) return BadRequest(new { message = "Your user account is not linked to an employee record." });

        try
        {
            var result = await _service.RaiseRequisitionAsync(
                id, dto ?? new RaiseRequisitionFromVacancyDto(), tenantId.Value, employeeId.Value, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Recomputes vacancies from live headcount (opens missing, closes filled). Admin action.</summary>
    [HttpPost("reconcile")]
    public async Task<ActionResult<ReconcileVacanciesResultDto>> Reconcile(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId ?? Guid.Empty;

        if (tenantId == null) return BadRequest(new { message = "Tenant context could not be resolved." });

        return Ok(await _service.ReconcilePositionVacanciesAsync(tenantId.Value, employeeId, ct));
    }

    public sealed class UpdateNotesRequest
    {
        public string? Notes { get; set; }
    }
}
