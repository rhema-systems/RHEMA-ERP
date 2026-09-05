using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-daily-attendance")]
[Authorize(Policy = "InternalOnly")]
public class StaffDailyAttendanceController : AttendanceControllerBase
{
    private readonly IStaffDailyAttendanceService _service;

    public StaffDailyAttendanceController(IStaffDailyAttendanceService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffDailyAttendanceSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    /// <summary>
    /// Filtered, sorted, paged search across the tenant's daily attendance.
    ///
    /// POST rather than GET because the filter carries a status list and a dozen tri-state
    /// flags; the page and size stay on the query string so a link can page without
    /// re-posting the body. This mirrors <c>POST api/hr/Employees/paged</c>.
    /// </summary>
    [HttpPost("search")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffDailyAttendanceSummaryDto>>> Search(
        [FromBody] StaffDailyAttendanceSearchDto filter,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.SearchAsync(filter ?? new StaffDailyAttendanceSearchDto(), pageNumber, pageSize, ct));

    // W3: self-or-permission — ownership is only knowable after the fetch.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffDailyAttendanceDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var record = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(record.EmployeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(record);
    }

    // W3: self-or-permission — an employee reads their own day.
    [HttpGet("employee/{employeeId:guid}/date/{date}")]
    public async Task<ActionResult<StaffDailyAttendanceDto?>> GetByEmployeeAndDate(
        Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeAndDateAsync(employeeId, date, ct));
    }

    // W3: self-or-permission — an employee reads their own attendance run.
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetByEmployeeId(
        Guid employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, from, to, ct));
    }

    [HttpGet("date/{date}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetByDate(
        DateOnly date, CancellationToken ct = default)
        => Ok(await _service.GetByDateAsync(date, ct));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetByStatus(
        StaffAttendanceStatus status,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, from, to, ct));

    [HttpGet("pending-verification")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetPendingVerification(CancellationToken ct = default)
        => Ok(await _service.GetPendingVerificationAsync(ct));

    [HttpGet("open-exceptions")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetWithOpenExceptions(CancellationToken ct = default)
        => Ok(await _service.GetWithOpenExceptionsAsync(ct));

    [HttpGet("overtime")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetWithOvertime(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? employeeId = null,
        CancellationToken ct = default)
        => Ok(await _service.GetWithOvertimeAsync(from, to, employeeId, ct));

    [HttpGet("late")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetLateAttendances(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? employeeId = null,
        CancellationToken ct = default)
        => Ok(await _service.GetLateAttendancesAsync(from, to, employeeId, ct));

    [HttpGet("remote-work")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetRemoteWorkDays(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? employeeId = null,
        CancellationToken ct = default)
        => Ok(await _service.GetRemoteWorkDaysAsync(from, to, employeeId, ct));

    [HttpGet("pay-period/{payPeriodId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffDailyAttendanceSummaryDto>>> GetByPayPeriodId(
        Guid payPeriodId, CancellationToken ct = default)
        => Ok(await _service.GetByPayPeriodIdAsync(payPeriodId, ct));

    [HttpPost]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffDailyAttendanceDto>> Create(
        [FromBody] CreateStaffDailyAttendanceDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffDailyAttendanceDto>> Update(
        Guid id, [FromBody] UpdateStaffDailyAttendanceDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("verify")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffDailyAttendanceDto>> Verify(
        [FromBody] VerifyAttendanceDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.VerifyAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/approve-exception")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffDailyAttendanceDto>> ApproveException(
        Guid id, [FromBody] ApproveAttendanceExceptionDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ApproveExceptionAsync(id, dto.Comments, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
