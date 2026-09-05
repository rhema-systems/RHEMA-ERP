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
[Route("api/shift-rotation-plans")]
[Authorize(Policy = "InternalOnly")]
public class ShiftRotationPlansController : AttendanceControllerBase
{
    private readonly IShiftRotationPlanService _service;

    public ShiftRotationPlansController(IShiftRotationPlanService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShiftRotationPlanSummaryDto>>> GetAll(CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<ShiftRotationPlanSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftRotationPlanDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ShiftRotationPlanSummaryDto>>> GetActivePlans(CancellationToken ct = default)
        => Ok(await _service.GetActivePlansAsync(ct));

    [HttpGet("cycle/{cycle}")]
    public async Task<ActionResult<IEnumerable<ShiftRotationPlanSummaryDto>>> GetByRotationCycle(
        ShiftRotationCycle cycle, CancellationToken ct = default)
        => Ok(await _service.GetByRotationCycleAsync(cycle, ct));

    [HttpGet("{id:guid}/stages")]
    public async Task<ActionResult<ShiftRotationPlanDto>> GetWithStages(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetWithStagesAsync(id, ct));

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<ShiftRotationPlanDto>> GetWithMembers(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetWithMembersAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftRotationPlanDto>> Create(
        [FromBody] CreateShiftRotationPlanDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftRotationPlanDto>> Update(
        Guid id, [FromBody] UpdateShiftRotationPlanDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/stages")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftRotationStageDto>> AddStage(
        Guid id, [FromBody] CreateShiftRotationStageDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        dto.ShiftRotationPlanId = id;
        return Ok(await _service.AddStageAsync(dto, tenantId, employeeId, ct));
    }

    [HttpGet("{id:guid}/stages/list")]
    public async Task<ActionResult<IEnumerable<ShiftRotationStageDto>>> GetStages(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetStagesAsync(id, ct));

    [HttpPut("stages/{stageId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftRotationStageDto>> UpdateStage(
        Guid stageId, [FromBody] UpdateShiftRotationStageDto dto, CancellationToken ct = default)
    {
        if (stageId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateStageAsync(dto, employeeId, ct));
    }

    [HttpDelete("stages/{stageId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> DeleteStage(Guid stageId, CancellationToken ct = default)
    {
        await _service.DeleteStageAsync(stageId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftRotationMemberDto>> EnrollMember(
        Guid id, [FromBody] AddShiftRotationMemberDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        dto.ShiftRotationPlanId = id;
        return Ok(await _service.EnrollMemberAsync(dto, tenantId, employeeId, ct));
    }

    /// <summary>Who is on this rotation.</summary>
    /// <remarks>
    /// Gated in area 25 slice 14, alongside the shift-assignment reads and for the same reason:
    /// the plan's STAGES are its structure and stay open like the rest of the scheduling
    /// registers, but its MEMBERS are named people and which pattern they work. Its own write
    /// sibling (<c>PUT members/{memberId}</c>) was already on AttendanceWrite; only the read
    /// was missed.
    /// </remarks>
    [HttpGet("{id:guid}/members/list")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ShiftRotationMemberDto>>> GetMembers(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetMembersAsync(id, ct));

    [HttpPut("members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftRotationMemberDto>> UpdateMember(
        Guid memberId, [FromBody] UpdateShiftRotationMemberDto dto, CancellationToken ct = default)
    {
        if (memberId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateMemberAsync(dto, employeeId, ct));
    }

    [HttpDelete("members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> RemoveMember(Guid memberId, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        await _service.RemoveMemberAsync(memberId, employeeId, ct);
        return NoContent();
    }
}
