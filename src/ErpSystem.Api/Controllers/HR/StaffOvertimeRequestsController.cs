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
[Route("api/staff-overtime-requests")]
[Authorize(Policy = "InternalOnly")]
public class StaffOvertimeRequestsController : AttendanceControllerBase
{
    private readonly IStaffOvertimeRequestService _service;

    public StaffOvertimeRequestsController(IStaffOvertimeRequestService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffOvertimeRequestSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    // W3: self-or-permission — ownership is only knowable after the fetch.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffOvertimeRequestDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var request = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(request.EmployeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(request);
    }

    // W3: self-or-permission — the number is not a capability.
    [HttpGet("number/{requestNumber}")]
    public async Task<ActionResult<StaffOvertimeRequestDto?>> GetByRequestNumber(
        string requestNumber, CancellationToken ct = default)
    {
        var request = await _service.GetByRequestNumberAsync(requestNumber, ct);
        if (request is null) return Ok(request);
        if (!await SelfOrPolicyAsync(request.EmployeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(request);
    }

    // W3: self-or-permission — an employee reads their own requests.
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffOvertimeRequestSummaryDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffOvertimeRequestSummaryDto>>> GetByStatus(
        OvertimeRequestStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("pending-approval")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffOvertimeRequestSummaryDto>>> GetPendingApproval(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingApprovalAsync(ct));

    [HttpGet("pending-supervisor-confirmation")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffOvertimeRequestSummaryDto>>> GetPendingSupervisorConfirmation(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingSupervisorConfirmationAsync(ct));

    [HttpGet("range")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffOvertimeRequestSummaryDto>>> GetByDateRange(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByDateRangeAsync(from, to, ct));

    [HttpPost]
    public async Task<ActionResult<StaffOvertimeRequestDto>> Create(
        [FromBody] CreateStaffOvertimeRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        // W3: the subject comes from the body, so filing overtime FOR someone else is a desk act
        // (the leave-request shape).
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.AttendanceWritePolicy))
            return Forbid();

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffOvertimeRequestDto>> Update(
        Guid id, [FromBody] UpdateStaffOvertimeRequestDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        // W3: the request's owner amends their pending request; otherwise the desk.
        var existing = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(existing.EmployeeId, HrPermissions.AttendanceWritePolicy))
            return Forbid();

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<StaffOvertimeRequestDto>> Approve(
        Guid id, [FromBody] ApproveOvertimeRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ApproveAsync(id, dto.ApprovalComments, employeeId, ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<StaffOvertimeRequestDto>> Reject(
        Guid id, [FromBody] RejectOvertimeRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.RejectAsync(id, dto.RejectionReason, employeeId, ct));
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffOvertimeRequestDto>> ConfirmActualHours(
        Guid id, [FromBody] ConfirmOvertimeRequestDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ConfirmActualHoursAsync(
            id, dto.ActualOvertimeHours, dto.SupervisorNotes, employeeId, ct));
    }

    // W3: withdrawal-shaped — the owner removes their own pending request, the desk anyone's.
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
