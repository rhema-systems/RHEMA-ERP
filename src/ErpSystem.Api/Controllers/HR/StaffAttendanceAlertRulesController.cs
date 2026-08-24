using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-attendance-alert-rules")]
[Authorize(Policy = "InternalOnly")]
public class StaffAttendanceAlertRulesController : AttendanceControllerBase
{
    private readonly IStaffAttendanceAlertRuleService _service;

    public StaffAttendanceAlertRulesController(
        IStaffAttendanceAlertRuleService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertRuleSummaryDto>>> GetAll(CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<StaffAttendanceAlertRuleSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceAlertRuleDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertRuleSummaryDto>>> GetActiveRules(
        CancellationToken ct = default)
        => Ok(await _service.GetActiveRulesAsync(ct));

    [HttpGet("type/{alertType}")]
    public async Task<ActionResult<IEnumerable<StaffAttendanceAlertRuleSummaryDto>>> GetByType(
        AttendanceAlertTriggerType alertType, CancellationToken ct = default)
        => Ok(await _service.GetByTypeAsync(alertType, ct));

    [HttpPost]
    public async Task<ActionResult<StaffAttendanceAlertRuleDto>> Create(
        [FromBody] CreateStaffAttendanceAlertRuleDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffAttendanceAlertRuleDto>> Update(
        Guid id, [FromBody] UpdateStaffAttendanceAlertRuleDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        await _service.ToggleActiveAsync(id, employeeId, ct);
        return Ok(new { message = "Alert rule toggled successfully." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
