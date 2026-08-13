using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/committees")]
[SafetyBusinessRules]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class SafetyCommitteeController : SheApiControllerBase
{
    private readonly ISafetyCommitteeService _service;

    public SafetyCommitteeController(ISafetyCommitteeService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Committees ──
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyCommitteeDto>> GetCommittee(Guid id)
        => Ok(await _service.GetCommitteeAsync(id));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<SafetyCommitteeDto>>> GetActiveCommittees()
        => Ok(await _service.GetActiveCommitteesAsync());

    [HttpPost]
    public async Task<ActionResult<SafetyCommitteeDto>> CreateCommittee([FromBody] CreateSafetyCommitteeDto dto)
    {
        var created = await _service.CreateCommitteeAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetCommittee), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetyCommitteeDto>> UpdateCommittee(Guid id, [FromBody] UpdateSafetyCommitteeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateCommitteeAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCommittee(Guid id)
    {
        await _service.DeleteCommitteeAsync(id);
        return NoContent();
    }

    // ── Members ──
    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IEnumerable<SafetyCommitteeMemberDto>>> GetMembers(Guid id)
        => Ok(await _service.GetMembersAsync(id));

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<SafetyCommitteeMemberDto>> AddMember(Guid id, [FromBody] CreateSafetyCommitteeMemberDto dto)
    {
        dto.CommitteeId = id;
        return Ok(await _service.AddMemberAsync(dto, TenantId, UserId));
    }

    [HttpPut("members/{memberId:guid}")]
    public async Task<ActionResult<SafetyCommitteeMemberDto>> UpdateMember(Guid memberId, [FromBody] UpdateSafetyCommitteeMemberDto dto)
    {
        if (memberId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateMemberAsync(dto, UserId));
    }

    [HttpDelete("members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid memberId)
    {
        await _service.RemoveMemberAsync(memberId);
        return NoContent();
    }

    // ── Meetings ──
    [HttpGet("meetings/{meetingId:guid}")]
    public async Task<ActionResult<SafetyMeetingDto>> GetMeeting(Guid meetingId)
        => Ok(await _service.GetMeetingAsync(meetingId));

    [HttpGet("{id:guid}/meetings")]
    public async Task<ActionResult<IEnumerable<SafetyMeetingSummaryDto>>> GetMeetingsByCommittee(Guid id)
        => Ok(await _service.GetMeetingsByCommitteeAsync(id));

    [HttpGet("meetings/date-range")]
    public async Task<ActionResult<IEnumerable<SafetyMeetingSummaryDto>>> GetMeetingsByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetMeetingsByDateRangeAsync(from, to));

    [HttpPost("meetings")]
    public async Task<ActionResult<SafetyMeetingDto>> CreateMeeting([FromBody] CreateSafetyMeetingDto dto)
    {
        var created = await _service.CreateMeetingAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetMeeting), new { meetingId = created.Id }, created);
    }

    [HttpPut("meetings/{meetingId:guid}")]
    public async Task<ActionResult<SafetyMeetingDto>> UpdateMeeting(Guid meetingId, [FromBody] UpdateSafetyMeetingDto dto)
    {
        if (meetingId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateMeetingAsync(dto, UserId));
    }

    [HttpDelete("meetings/{meetingId:guid}")]
    public async Task<IActionResult> DeleteMeeting(Guid meetingId)
    {
        await _service.DeleteMeetingAsync(meetingId);
        return NoContent();
    }

    // ── Attendees ──
    [HttpPost("meetings/{meetingId:guid}/attendees")]
    public async Task<ActionResult<SafetyMeetingAttendeeDto>> AddAttendee(Guid meetingId, [FromBody] CreateSafetyMeetingAttendeeDto dto)
    {
        dto.MeetingId = meetingId;
        return Ok(await _service.AddAttendeeAsync(dto, TenantId, UserId));
    }

    [HttpDelete("attendees/{attendeeId:guid}")]
    public async Task<IActionResult> RemoveAttendee(Guid attendeeId)
    {
        await _service.RemoveAttendeeAsync(attendeeId);
        return NoContent();
    }

    // ── Action items ──
    [HttpGet("action-items/open")]
    public async Task<ActionResult<IEnumerable<SafetyMeetingActionItemDto>>> GetOpenActionItems()
        => Ok(await _service.GetOpenActionItemsAsync());

    [HttpGet("action-items/overdue")]
    public async Task<ActionResult<IEnumerable<SafetyMeetingActionItemDto>>> GetOverdueActionItems()
        => Ok(await _service.GetOverdueActionItemsAsync());

    [HttpGet("action-items/by-assignee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyMeetingActionItemDto>>> GetActionItemsByAssignee(Guid employeeId)
        => Ok(await _service.GetActionItemsByAssigneeAsync(employeeId));

    [HttpPost("meetings/{meetingId:guid}/action-items")]
    public async Task<ActionResult<SafetyMeetingActionItemDto>> AddActionItem(Guid meetingId, [FromBody] CreateSafetyMeetingActionItemDto dto)
    {
        dto.MeetingId = meetingId;
        return Ok(await _service.AddActionItemAsync(dto, TenantId, UserId));
    }

    [HttpPut("action-items/{actionItemId:guid}")]
    public async Task<ActionResult<SafetyMeetingActionItemDto>> UpdateActionItem(Guid actionItemId, [FromBody] UpdateSafetyMeetingActionItemDto dto)
    {
        if (actionItemId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateActionItemAsync(dto, UserId));
    }

    [HttpDelete("action-items/{actionItemId:guid}")]
    public async Task<IActionResult> DeleteActionItem(Guid actionItemId)
    {
        await _service.DeleteActionItemAsync(actionItemId);
        return NoContent();
    }

    // ── Documents ──
    [HttpPost("meetings/{meetingId:guid}/documents")]
    public async Task<ActionResult<SafetyMeetingDocumentDto>> AddMeetingDocument(Guid meetingId, [FromBody] CreateSafetyMeetingDocumentDto dto)
    {
        dto.MeetingId = meetingId;
        return Ok(await _service.AddMeetingDocumentAsync(dto, TenantId, UserId));
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteMeetingDocument(Guid documentId)
    {
        await _service.DeleteMeetingDocumentAsync(documentId);
        return NoContent();
    }
}
