using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-attendance-records")]
[Authorize(Policy = "InternalOnly")]
public class StaffAttendanceRecordsController : AttendanceControllerBase
{
    private readonly IStaffAttendanceRecordService _service;

    public StaffAttendanceRecordsController(IStaffAttendanceRecordService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<StaffAttendanceRecordSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceRecordDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}/date/{date}")]
    public async Task<ActionResult<StaffAttendanceRecordDto?>> GetByEmployeeAndDate(
        Guid employeeId, DateOnly date, CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeAndDateAsync(employeeId, date, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRecordSummaryDto>>> GetByEmployeeId(
        Guid employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, from, to, ct));

    [HttpGet("date/{date}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRecordSummaryDto>>> GetByDate(
        DateOnly date, CancellationToken ct = default)
        => Ok(await _service.GetByDateAsync(date, ct));

    [HttpGet("range")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRecordSummaryDto>>> GetByDateRange(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByDateRangeAsync(from, to, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRecordSummaryDto>>> GetByStatus(
        StaffAttendanceStatus status,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, from, to, ct));

    [HttpPost]
    public async Task<ActionResult<StaffAttendanceRecordDto>> Create(
        [FromBody] CreateStaffAttendanceRecordDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceRecordDto>> Update(
        Guid id, [FromBody] UpdateStaffAttendanceRecordDto dto, CancellationToken ct = default)
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
