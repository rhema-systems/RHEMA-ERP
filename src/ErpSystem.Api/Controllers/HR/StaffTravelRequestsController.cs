using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/requests")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelReadPolicy)]
public class StaffTravelRequestsController : HrControllerBase
{
    private readonly IStaffTravelRequestService _service;

    public StaffTravelRequestsController(IStaffTravelRequestService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffTravelRequestSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffTravelRequestDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{requestNumber}")]
    public async Task<ActionResult<StaffTravelRequestDto?>> GetByRequestNumber(string requestNumber)
        => Ok(await _service.GetByRequestNumberAsync(requestNumber));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetByStatus(StaffTravelRequestStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetByDateRange(
        [FromQuery] DateOnly start, [FromQuery] DateOnly end)
        => Ok(await _service.GetByDateRangeAsync(start, end));

    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetByOrganizationUnit(Guid organizationUnitId)
        => Ok(await _service.GetByOrganizationUnitAsync(organizationUnitId));

    [HttpGet("pending-approval")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetPendingApproval()
        => Ok(await _service.GetPendingApprovalAsync());

    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetUpcoming([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetUpcomingTripsAsync(daysAhead));

    [HttpGet("{parentRequestId:guid}/children")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestSummaryDto>>> GetChildren(Guid parentRequestId)
        => Ok(await _service.GetChildRequestsAsync(parentRequestId));

    [HttpGet("dashboard")]
    public async Task<ActionResult<StaffTravelDashboardDto>> GetDashboard([FromQuery] int upcomingDays = 30)
        => Ok(await _service.GetDashboardAsync(upcomingDays));

    // =========================================================================
    // CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<StaffTravelRequestDto>> Create([FromBody] CreateStaffTravelRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        var created = await _service.CreateAsync(dto, tenantId, userId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffTravelRequestDto>> Update(Guid id, [FromBody] UpdateStaffTravelRequestDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        await _service.SubmitAsync(new SubmitStaffTravelRequestDto { RequestId = id, SubmittedById = userId });
        return Ok(new { message = "Travel request submitted." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveStaffTravelRequestDto dto)
    {
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        dto.RequestId = id;
        dto.ApprovedById = userId;
        await _service.ApproveAsync(dto);
        return Ok(new { message = "Travel request approved." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromQuery] string? reason = null)
    {
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        await _service.RejectAsync(id, userId, reason);
        return Ok(new { message = "Travel request rejected." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelStaffTravelRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Two ids, deliberately: CancelledById is an Employee FK on the request, userId is the
        // audit trail. Slice 0 collapsed both onto the user id and broke the FK.
        if (TryGetEmployeeWriteContext(out _, out var userId, out var employeeId,
                "Cancelling a travel request") is { } contextError) return contextError;

        dto.RequestId = id;
        dto.CancelledById = employeeId;
        await _service.CancelAsync(dto, userId);
        return Ok(new { message = "Travel request cancelled." });
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        await _service.MarkCompletedAsync(id, userId);
        return Ok(new { message = "Travel request marked as completed." });
    }

    // =========================================================================
    // COMMENTS
    // =========================================================================

    [HttpGet("{requestId:guid}/comments")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestCommentDto>>> GetComments(Guid requestId)
        => Ok(await _service.GetCommentsAsync(requestId));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{requestId:guid}/comments")]
    public async Task<ActionResult<StaffTravelRequestCommentDto>> AddComment(Guid requestId, [FromBody] CreateStaffTravelRequestCommentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // AuthorId is an Employee FK, so this is one of the few travel writes that genuinely needs
        // the caller's employee link — the comment records who said it.
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Commenting on a travel request") is { } contextError) return contextError;

        dto.StaffTravelRequestId = requestId;
        return Ok(await _service.AddCommentAsync(dto, tenantId, userId, employeeId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("comments/{commentId:guid}")]
    public async Task<ActionResult<StaffTravelRequestCommentDto>> UpdateComment(Guid commentId, [FromBody] UpdateStaffTravelRequestCommentDto dto)
    {
        if (commentId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateCommentAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId)
    {
        await _service.DeleteCommentAsync(commentId);
        return NoContent();
    }

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    [HttpGet("{requestId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestAttachmentDto>>> GetAttachments(Guid requestId)
        => Ok(await _service.GetAttachmentsAsync(requestId));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{requestId:guid}/attachments")]
    public async Task<ActionResult<StaffTravelRequestAttachmentDto>> AddAttachment(Guid requestId, [FromBody] CreateStaffTravelRequestAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // UploadedById is an Employee FK — same reasoning as comments.
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Attaching a document to a travel request") is { } contextError) return contextError;

        dto.StaffTravelRequestId = requestId;
        return Ok(await _service.AddAttachmentAsync(dto, tenantId, userId, employeeId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        await _service.DeleteAttachmentAsync(attachmentId);
        return NoContent();
    }

    // =========================================================================
    // GROUP TRAVEL
    // =========================================================================

    [HttpGet("groups")]
    public async Task<ActionResult<IEnumerable<StaffGroupTravelSummaryDto>>> GetAllGroups()
        => Ok(await _service.GetAllGroupTravelsAsync());

    [HttpGet("groups/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffGroupTravelSummaryDto>>> GetGroupsByStatus(GroupTravelStatus status)
        => Ok(await _service.GetGroupTravelsByStatusAsync(status));

    [HttpGet("groups/{id:guid}")]
    public async Task<ActionResult<StaffGroupTravelDto>> GetGroupById(Guid id)
        => Ok(await _service.GetGroupTravelByIdAsync(id));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("groups")]
    public async Task<ActionResult<StaffGroupTravelDto>> CreateGroup([FromBody] CreateStaffGroupTravelDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        var created = await _service.CreateGroupTravelAsync(dto, tenantId, userId);
        return CreatedAtAction(nameof(GetGroupById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("groups/{id:guid}")]
    public async Task<ActionResult<StaffGroupTravelDto>> UpdateGroup(Guid id, [FromBody] UpdateStaffGroupTravelDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateGroupTravelAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("groups/{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        await _service.DeleteGroupTravelAsync(id);
        return NoContent();
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("groups/{id:guid}/participants")]
    public async Task<ActionResult<StaffGroupTravelDto>> AddGroupParticipants(Guid id, [FromBody] AddGroupTravelParticipantsDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Each participant's request records who initiated it — an Employee FK — so this is
        // another of the few travel writes that genuinely needs the caller's employee link.
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Adding participants to a group trip") is { } contextError) return contextError;

        dto.GroupTravelId = id;
        return Ok(await _service.AddGroupParticipantsAsync(dto, tenantId, userId, employeeId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("groups/{groupId:guid}/participants/{requestId:guid}")]
    public async Task<IActionResult> RemoveGroupParticipant(Guid groupId, Guid requestId)
    {
        var removed = await _service.RemoveGroupParticipantAsync(groupId, requestId);
        return removed ? NoContent() : NotFound();
    }
}
