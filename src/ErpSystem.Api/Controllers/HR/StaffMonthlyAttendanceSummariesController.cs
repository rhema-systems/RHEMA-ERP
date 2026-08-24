using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-monthly-attendance-summaries")]
[Authorize(Policy = "InternalOnly")]
public class StaffMonthlyAttendanceSummariesController : AttendanceControllerBase
{
    private readonly IStaffMonthlyAttendanceSummaryService _service;

    public StaffMonthlyAttendanceSummariesController(
        IStaffMonthlyAttendanceSummaryService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffMonthlyAttendanceSummaryDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}/period")]
    public async Task<ActionResult<StaffMonthlyAttendanceSummaryDto?>> GetByEmployeeAndPeriod(
        Guid employeeId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeAndPeriodAsync(employeeId, year, month, ct));

    [HttpGet("employee/{employeeId:guid}/year/{year:int}")]
    public async Task<ActionResult<IEnumerable<StaffMonthlyAttendanceSummaryDto>>> GetByEmployeeAndYear(
        Guid employeeId, int year, CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeAndYearAsync(employeeId, year, ct));

    [HttpGet("year/{year:int}/month/{month:int}")]
    public async Task<ActionResult<IEnumerable<StaffMonthlyAttendanceSummaryDto>>> GetByYearAndMonth(
        int year, int month, CancellationToken ct = default)
        => Ok(await _service.GetByYearAndMonthAsync(year, month, ct));

    [HttpGet("pay-period/{payPeriodId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMonthlyAttendanceSummaryDto>>> GetByPayPeriodId(
        Guid payPeriodId, CancellationToken ct = default)
        => Ok(await _service.GetByPayPeriodIdAsync(payPeriodId, ct));

    [HttpGet("unfinalized")]
    public async Task<ActionResult<IEnumerable<StaffMonthlyAttendanceSummaryDto>>> GetUnfinalized(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
        => Ok(await _service.GetUnfinalizedAsync(year, month, ct));

    [HttpPost("recalculate")]
    public async Task<ActionResult<StaffMonthlyAttendanceSummaryDto>> Recalculate(
        [FromQuery] Guid employeeId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        if (TryGetEmployee(out var actorId) is { } error) return error;

        return Ok(await _service.RecalculateAsync(employeeId, year, month, actorId, ct));
    }

    [HttpPost("{id:guid}/finalize")]
    public async Task<ActionResult<StaffMonthlyAttendanceSummaryDto>> Finalize(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.FinalizeAsync(id, employeeId, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffMonthlyAttendanceSummaryDto>> Update(
        Guid id, [FromBody] UpdateStaffMonthlyAttendanceSummaryDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
