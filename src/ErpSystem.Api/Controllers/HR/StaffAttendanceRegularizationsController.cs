using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-attendance-regularizations")]
[Authorize(Policy = "InternalOnly")]
public class StaffAttendanceRegularizationsController : AttendanceControllerBase
{
    private readonly IStaffAttendanceRegularizationService _service;

    public StaffAttendanceRegularizationsController(
        IStaffAttendanceRegularizationService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffAttendanceRegularizationSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    // W3: self-or-permission — ownership is only knowable after the fetch.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceRegularizationDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var dto = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(dto);
    }

    // W3: self-or-permission — the number is not a capability.
    [HttpGet("number/{regularizationNumber}")]
    public async Task<ActionResult<StaffAttendanceRegularizationDto?>> GetByRegularizationNumber(
        string regularizationNumber, CancellationToken ct = default)
    {
        var dto = await _service.GetByRegularizationNumberAsync(regularizationNumber, ct);
        if (dto is null) return Ok(dto);
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(dto);
    }

    // W3: self-or-permission — an employee reads their own regularizations.
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRegularizationSummaryDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("attendance/{attendanceId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRegularizationSummaryDto>>> GetByAttendanceId(
        Guid attendanceId, CancellationToken ct = default)
        => Ok(await _service.GetByAttendanceIdAsync(attendanceId, ct));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRegularizationSummaryDto>>> GetByStatus(
        AttendanceRegularizationStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("pending-approval")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceRegularizationSummaryDto>>> GetPendingApproval(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingApprovalAsync(ct));

    [HttpPost]
    public async Task<ActionResult<StaffAttendanceRegularizationDto>> Create(
        [FromBody] CreateStaffAttendanceRegularizationDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceRegularizationDto>> Update(
        Guid id, [FromBody] UpdateStaffAttendanceRegularizationDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        // W3: the request's owner amends their pending regularization; otherwise the desk.
        // The service checks the status but not the caller.
        var existing = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(existing.EmployeeId, HrPermissions.AttendanceWritePolicy))
            return Forbid();

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    // W3: approve/reject deliberately NOT permission-gated - the workflow assignee's act, validated per request by the service.
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<StaffAttendanceRegularizationDto>> Approve(
        Guid id, [FromBody] ApproveRegularizationDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        dto.RegularizationId = id;
        dto.ApprovedById = employeeId;
        return Ok(await _service.ApproveAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<StaffAttendanceRegularizationDto>> Reject(
        Guid id, [FromBody] RejectRegularizationDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        dto.RegularizationId = id;
        return Ok(await _service.RejectAsync(dto, employeeId, ct));
    }

    // W3: applying posts an already-approved decision onto the attendance record - the desk's act.
    [HttpPost("{id:guid}/apply")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<IActionResult> Apply(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        await _service.ApplyAsync(id, employeeId, ct);
        return Ok(new { message = "Regularization applied successfully." });
    }

    // W3: withdrawal-shaped — the owner removes their own un-applied request, the desk anyone's.
    // The service refuses applied deletions; the caller check is here.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var existing = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(existing.EmployeeId, HrPermissions.AttendanceWritePolicy))
            return Forbid();

        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
