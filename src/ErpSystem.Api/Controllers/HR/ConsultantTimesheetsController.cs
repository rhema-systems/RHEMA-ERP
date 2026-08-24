using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/consultant-timesheets")]
[Authorize(Policy = "InternalOnly")]
public class ConsultantTimesheetsController : AttendanceControllerBase
{
    private readonly IConsultantTimesheetService _service;

    public ConsultantTimesheetsController(IConsultantTimesheetService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<ConsultantTimesheetSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    /// <summary>
    /// Self-or-permission resolved through the timesheet's consultant (an Employee FK).
    /// </summary>
    private async Task<bool> CanActOnTimesheetAsync(Guid timesheetId, string policy, CancellationToken ct)
    {
        if (CurrentUser.EmployeeId is Guid me && me != Guid.Empty &&
            CurrentUser.TenantId is Guid tenantId)
        {
            var db = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var mine = await db.Set<Core.Entities.HR.StaffAttendance.ConsultantTimesheet>()
                .AsNoTracking()
                .AnyAsync(t => t.Id == timesheetId && t.TenantId == tenantId && t.ConsultantId == me, ct);
            if (mine) return true;
        }
        return await HoldsPolicyAsync(policy);
    }

    /// <summary>Self-or-permission resolved from an entry up through its timesheet.</summary>
    private async Task<bool> CanActOnEntryAsync(Guid entryId, string policy, CancellationToken ct)
    {
        if (CurrentUser.EmployeeId is Guid me && me != Guid.Empty &&
            CurrentUser.TenantId is Guid tenantId)
        {
            var db = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var mine = await db.Set<Core.Entities.HR.StaffAttendance.ConsultantTimesheetEntry>()
                .AsNoTracking()
                .Where(e => e.Id == entryId && e.TenantId == tenantId)
                .Join(db.Set<Core.Entities.HR.StaffAttendance.ConsultantTimesheet>().AsNoTracking(),
                    e => e.TimesheetId, t => t.Id, (e, t) => t)
                .AnyAsync(t => t.ConsultantId == me, ct);
            if (mine) return true;
        }
        return await HoldsPolicyAsync(policy);
    }

    // W3: self-or-permission — ownership is only knowable after the fetch.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConsultantTimesheetDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var dto = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(dto.ConsultantId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(dto);
    }

    // W3: self-or-permission — the number is not a capability.
    [HttpGet("number/{timesheetNumber}")]
    public async Task<ActionResult<ConsultantTimesheetDto?>> GetByTimesheetNumber(
        string timesheetNumber, CancellationToken ct = default)
    {
        var dto = await _service.GetByTimesheetNumberAsync(timesheetNumber, ct);
        if (dto is null) return Ok(dto);
        if (!await SelfOrPolicyAsync(dto.ConsultantId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(dto);
    }

    // W3: self-or-permission — a consultant reads their own timesheets.
    [HttpGet("consultant/{consultantEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByConsultantId(
        Guid consultantEmployeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(consultantEmployeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetByConsultantIdAsync(consultantEmployeeId, ct));
    }

    [HttpGet("engagement/{engagementId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByEngagementId(
        Guid engagementId, CancellationToken ct = default)
        => Ok(await _service.GetByEngagementIdAsync(engagementId, ct));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByStatus(
        TimesheetStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("period")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetByPeriod(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
        => Ok(await _service.GetByPeriodAsync(from, to, ct));

    [HttpGet("pending-approval")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetPendingApproval(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingApprovalAsync(ct));

    [HttpGet("pending-client-confirmation")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetPendingClientConfirmation(
        CancellationToken ct = default)
        => Ok(await _service.GetPendingClientConfirmationAsync(ct));

    [HttpGet("approved-for-invoicing")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetSummaryDto>>> GetApprovedForInvoicing(
        [FromQuery] Guid? clientId = null, CancellationToken ct = default)
        => Ok(await _service.GetApprovedForInvoicingAsync(clientId, ct));

    [HttpPost]
    public async Task<ActionResult<ConsultantTimesheetDto>> Create(
        [FromBody] CreateConsultantTimesheetDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // W3: the timesheet's consultant amends their own; otherwise the desk.
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Update(
        Guid id, [FromBody] UpdateConsultantTimesheetDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    // W3: the consultant submits their own; the desk may submit on their behalf.
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Submit(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        return Ok(await _service.SubmitAsync(id, employeeId, ct));
    }

    // W3: approve/reject deliberately NOT permission-gated - the workflow assignee's act, validated per request by the service.
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Approve(
        Guid id, [FromBody] ApproveTimesheetRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ApproveAsync(id, request.Comments, employeeId, ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ConsultantTimesheetDto>> Reject(
        Guid id, [FromBody] RejectTimesheetRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.RejectAsync(id, request.RejectionReason, employeeId, ct));
    }

    // W3: withdrawal-shaped — the consultant removes their own, the desk anyone's.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // W3: entries belong to the timesheet's consultant; the desk may correct anyone's.
    [HttpPost("{id:guid}/entries")]
    public async Task<ActionResult<ConsultantTimesheetEntryDto>> AddEntry(
        Guid id, [FromBody] CreateConsultantTimesheetEntryDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        dto.TimesheetId = id;
        return Ok(await _service.AddEntryAsync(dto, tenantId, employeeId, ct));
    }

    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<IEnumerable<ConsultantTimesheetEntryDto>>> GetEntries(
        Guid id, CancellationToken ct = default)
    {
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceReadPolicy, ct))
            return Forbid();
        return Ok(await _service.GetEntriesAsync(id, ct));
    }

    // W3: ownership resolved from the entry up through its timesheet — the route's entryId is
    // the only trusted input; the dto's TimesheetId is client-controlled.
    [HttpPut("entries/{entryId:guid}")]
    public async Task<ActionResult<ConsultantTimesheetEntryDto>> UpdateEntry(
        Guid entryId, [FromBody] UpdateConsultantTimesheetEntryDto dto, CancellationToken ct = default)
    {
        if (entryId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;
        if (!await CanActOnEntryAsync(entryId, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        return Ok(await _service.UpdateEntryAsync(dto, employeeId, ct));
    }

    [HttpDelete("entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid entryId, CancellationToken ct = default)
    {
        if (!await CanActOnEntryAsync(entryId, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        await _service.DeleteEntryAsync(entryId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/send-confirmation")]
    public async Task<ActionResult<SendTimesheetConfirmationResultDto>> SendConfirmation(
        Guid id,
        [FromBody] SendTimesheetConfirmationRequestDto request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;
        // W3: sending the client-confirmation email is the consultant's or the desk's act.
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        try
        {
            return Ok(await _service.SendConfirmationAsync(id, request, tenantId, employeeId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/resend-confirmation")]
    public async Task<ActionResult<SendTimesheetConfirmationResultDto>> ResendConfirmation(
        Guid id,
        CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceWritePolicy, ct))
            return Forbid();

        try
        {
            return Ok(await _service.ResendConfirmationAsync(id, employeeId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/confirmation")]
    public async Task<ActionResult<ClientTimesheetConfirmationDto?>> GetConfirmation(
        Guid id, CancellationToken ct = default)
    {
        if (!await CanActOnTimesheetAsync(id, HrPermissions.AttendanceReadPolicy, ct))
            return Forbid();
        return Ok(await _service.GetConfirmationAsync(id, ct));
    }

    public sealed class ApproveTimesheetRequest
    {
        public string? Comments { get; set; }
    }

    public sealed class RejectTimesheetRequest
    {
        public string RejectionReason { get; set; } = string.Empty;
    }
}
