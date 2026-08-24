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
[Route("api/staff-attendance-alerts")]
[Authorize(Policy = "InternalOnly")]
public class StaffAttendanceAlertsController : AttendanceControllerBase
{
    private readonly IStaffAttendanceAlertService _service;

    public StaffAttendanceAlertsController(IStaffAttendanceAlertService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffAttendanceAlertSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<StaffAttendanceAlertDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    // W3: self-or-permission — an employee sees the alerts raised about them.
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertSummaryDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("rule/{ruleId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertSummaryDto>>> GetByRuleId(
        Guid ruleId, CancellationToken ct = default)
        => Ok(await _service.GetByRuleIdAsync(ruleId, ct));

    [HttpGet("unacknowledged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertSummaryDto>>> GetUnacknowledgedAlerts(
        CancellationToken ct = default)
        => Ok(await _service.GetUnacknowledgedAlertsAsync(ct));

    // W3: self-or-permission, as above.
    [HttpGet("employee/{employeeId:guid}/unacknowledged")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertSummaryDto>>> GetUnacknowledgedForEmployee(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetUnacknowledgedForEmployeeAsync(employeeId, ct));
    }

    [HttpGet("date/{date}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertSummaryDto>>> GetByAttendanceDate(
        DateOnly date, CancellationToken ct = default)
        => Ok(await _service.GetByAttendanceDateAsync(date, ct));

    [HttpGet("type/{alertType}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertSummaryDto>>> GetByType(
        AttendanceAlertTriggerType alertType, CancellationToken ct = default)
        => Ok(await _service.GetByTypeAsync(alertType, ct));

    [HttpGet("severity/{severity}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertSummaryDto>>> GetBySeverity(
        AttendanceAlertSeverity severity, CancellationToken ct = default)
        => Ok(await _service.GetBySeverityAsync(severity, ct));

    [HttpPost("evaluate/{dailyAttendanceId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertDto>>> EvaluateForAttendance(
        Guid dailyAttendanceId, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.EvaluateForAttendanceAsync(dailyAttendanceId, employeeId, ct));
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<StaffAttendanceAlertDto>> Acknowledge(
        Guid id, [FromBody] AcknowledgeAlertDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.AcknowledgeAsync(id, dto.AcknowledgementNotes, employeeId, ct));
    }

    [HttpPost("bulk-acknowledge")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<int>> BulkAcknowledge(
        [FromBody] BulkAcknowledgeAlertsRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        var count = await _service.BulkAcknowledgeAsync(request.AlertIds, request.Comments, employeeId, ct);
        return Ok(new { acknowledgedCount = count });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    public sealed class BulkAcknowledgeAlertsRequest
    {
        public IEnumerable<Guid> AlertIds { get; set; } = Array.Empty<Guid>();
        public string? Comments { get; set; }
    }
}
