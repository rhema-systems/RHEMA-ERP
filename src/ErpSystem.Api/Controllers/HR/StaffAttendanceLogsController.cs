using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-attendance-logs")]
[Authorize]
public class StaffAttendanceLogsController : AttendanceControllerBase
{
    private readonly IStaffAttendanceLogService _service;

    public StaffAttendanceLogsController(IStaffAttendanceLogService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<StaffAttendanceLogSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceLogDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceLogSummaryDto>>> GetByEmployeeId(
        Guid employeeId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, from, to, ct));

    [HttpGet("unprocessed")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceLogSummaryDto>>> GetUnprocessedLogs(CancellationToken ct = default)
        => Ok(await _service.GetUnprocessedLogsAsync(ct));

    [HttpGet("device/{deviceId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceLogSummaryDto>>> GetByDeviceId(
        Guid deviceId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct = default)
        => Ok(await _service.GetByDeviceIdAsync(deviceId, from, to, ct));

    [HttpPost]
    public async Task<ActionResult<StaffAttendanceLogDto>> Create(
        [FromBody] CreateStaffAttendanceLogDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("punch")]
    public async Task<ActionResult<StaffAttendancePunchResultDto>> Punch(
        [FromBody] StaffAttendancePunchDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var result = await _service.PunchAsync(dto, tenantId, employeeId, employeeId, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult<Guid?>> ProcessLog(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        var attendanceId = await _service.ProcessLogAsync(id, employeeId, ct);
        return Ok(new { attendanceId });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
