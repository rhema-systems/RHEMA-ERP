using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/succession-development")]
[Authorize(Policy = HrPermissions.SuccessionReadPolicy)]
public class SuccessionDevelopmentController : ControllerBase
{
    private readonly ISuccessionDevelopmentActivityService _service;
    private readonly ICurrentUserService _currentUser;

    public SuccessionDevelopmentController(ISuccessionDevelopmentActivityService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // DEVELOPMENT ACTIVITIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SuccessionDevelopmentActivityDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("candidate/{candidateId:guid}")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivitySummaryDto>>> GetByCandidate(Guid candidateId)
        => Ok(await _service.GetByCandidateIdAsync(candidateId));

    [HttpGet("candidate/{candidateId:guid}/full")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivityDto>>> GetByCandidateFull(Guid candidateId)
        => Ok(await _service.GetFullByCandidateIdAsync(candidateId));

    [HttpGet("member/{memberId:guid}")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivitySummaryDto>>> GetByMember(Guid memberId)
        => Ok(await _service.GetByTalentPoolMemberIdAsync(memberId));

    [HttpGet("member/{memberId:guid}/full")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivityDto>>> GetByMemberFull(Guid memberId)
        => Ok(await _service.GetFullByTalentPoolMemberIdAsync(memberId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivitySummaryDto>>> GetByStatus(DevelopmentActivityStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentActivitySummaryDto>>> GetOverdue()
        => Ok(await _service.GetOverdueActivitiesAsync());

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<SuccessionDevelopmentActivityDto>> Create([FromBody] CreateSuccessionDevelopmentActivityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SuccessionDevelopmentActivityDto>> Update(Guid id, [FromBody] UpdateSuccessionDevelopmentActivityDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // MILESTONES
    // =========================================================================

    [HttpGet("{activityId:guid}/milestones")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentMilestoneDto>>> GetMilestones(Guid activityId)
        => Ok(await _service.GetMilestonesAsync(activityId));

    [HttpGet("milestones/overdue")]
    public async Task<ActionResult<IEnumerable<SuccessionDevelopmentMilestoneDto>>> GetOverdueMilestones()
        => Ok(await _service.GetOverdueMilestonesAsync());

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("{activityId:guid}/milestones")]
    public async Task<ActionResult<SuccessionDevelopmentMilestoneDto>> AddMilestone(
        Guid activityId, [FromBody] CreateSuccessionDevelopmentMilestoneDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.ActivityId = activityId;
        var created = await _service.AddMilestoneAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetMilestones), new { activityId }, created);
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPut("milestones/{milestoneId:guid}")]
    public async Task<ActionResult<SuccessionDevelopmentMilestoneDto>> UpdateMilestone(
        Guid milestoneId, [FromBody] UpdateSuccessionDevelopmentMilestoneDto dto)
    {
        if (milestoneId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateMilestoneAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.SuccessionWritePolicy)]
    [HttpPost("milestones/{milestoneId:guid}/complete")]
    public async Task<IActionResult> CompleteMilestone(Guid milestoneId)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CompleteMilestoneAsync(milestoneId, employeeId.Value);
        return Ok(new { message = "Milestone marked as complete." });
    }

    [Authorize(Policy = HrPermissions.SuccessionAdminPolicy)]
    [HttpDelete("milestones/{milestoneId:guid}")]
    public async Task<IActionResult> DeleteMilestone(Guid milestoneId)
    {
        await _service.DeleteMilestoneAsync(milestoneId);
        return NoContent();
    }
}
