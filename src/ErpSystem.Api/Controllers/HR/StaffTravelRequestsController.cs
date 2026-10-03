using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Shared;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/requests")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelReadPolicy)]
public class StaffTravelRequestsController : HrControllerBase
{
    private readonly IStaffTravelRequestService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<StaffTravelRequestsController> _logger;

    public StaffTravelRequestsController(
        IStaffTravelRequestService service,
        IHrControlledDocumentService hrDocuments,
        IAuthorizationService authorization,
        ILogger<StaffTravelRequestsController> logger,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _authorization = authorization;
        _logger = logger;
    }

    /// <summary>
    /// Whether the caller holds <c>HR.Travel.Admin</c> — evaluated against the same policy the
    /// <c>[Authorize]</c> attributes use. (Since lane 4, D-3, the HR desk holds it.)
    /// </summary>
    private async Task<bool> CallerIsTravelAdminAsync()
        => (await _authorization.AuthorizeAsync(User, HrPermissions.TravelAdminPolicy)).Succeeded;

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

    // GET {id}, its comments, its attachments and their download are the approver's door since lane 2
    // (D-7): StaffTravelApprovalsController, same route, behind InternalOnly — this class's Read policy
    // would have kept a line manager out.

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

    /// <summary>
    /// The approved policy a trip for this traveller would be checked against, and its limits — what
    /// the request form shows before the desk saves anything (finding T-16).
    /// </summary>
    [HttpGet("policy-preview")]
    public async Task<ActionResult<StaffTravelPolicyPreviewDto>> GetPolicyPreview(
        [FromQuery] Guid employeeId, [FromQuery] DateOnly departure,
        [FromQuery] Guid? originCountryId = null, [FromQuery] Guid? destinationCountryId = null,
        CancellationToken ct = default)
        => Ok(await _service.GetPolicyPreviewAsync(employeeId, departure, originCountryId, destinationCountryId, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    /// <summary>Raise a travel request, normally on someone else's behalf.</summary>
    /// <remarks>
    /// <para><b><c>InitiatedById</c> on the payload is ignored.</b> It is an <c>Employee</c> FK
    /// recording who <i>raised</i> the request, so it is the caller — not the traveller, since the
    /// desk raises travel for other people, and not the payload, since accepting it let any caller
    /// file travel under a colleague's name. This is the same actor hole slice 0 closed across the
    /// area; it survived because the census counted controller parameters and this one travels
    /// inside the DTO.</para>
    ///
    /// <para>An administrative account with no employee link falls back to the traveller, which
    /// records the request as self-initiated. That is preferable to refusing the desk over a field
    /// it was never able to supply — see <see cref="HrControllerBase"/> on unlinked accounts.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<StaffTravelRequestDto>> Create([FromBody] CreateStaffTravelRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        dto.InitiatedById = CurrentUser.EmployeeId ?? dto.EmployeeId;

        var created = await _service.CreateAsync(dto, tenantId, userId);
        // The read lives on the approver's door since lane 2 — same route, the other controller.
        return CreatedAtAction(nameof(StaffTravelApprovalsController.GetById), "StaffTravelApprovals",
            new { id = created.Id }, created);
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

    /// <summary>Send a request for approval.</summary>
    /// <remarks>
    /// The body is optional and carries one thing: the desk's reason for submitting a trip whose
    /// departure date has passed (lane 1). Without it such a trip is refused; with it the reason is
    /// kept as an internal note in the submitter's name, so the caller must be linked to an employee.
    /// The answer says where the request now is and lists any warnings — approved leave over the same
    /// days, for instance.
    /// </remarks>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<StaffTravelSubmitResultDto>> Submit(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SubmitStaffTravelRequestBodyDto? body)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out _) is { } contextError) return contextError;

        return Ok(await _service.SubmitAsync(new SubmitStaffTravelRequestDto
        {
            RequestId = id,
            LateSubmissionReason = body?.LateSubmissionReason,
            SubmittedByEmployeeId = CurrentUser.EmployeeId,
        }));
    }

    // Approve, reject and return for revision are the approver's since lane 2 (D-7) — the traveller's
    // line authority, then HR — and live on StaffTravelApprovalsController, not behind this class's
    // Read policy and the Write policy they used to carry.

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
        // Lane 8 (D-48): this is the desk's door, so a trip under way may be cancelled here as not travelled.
        await _service.CancelAsync(dto, userId, HttpContext.RequestAborted, callerIsTravelDesk: true);
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

    /// <summary>Ask for a change to an approved trip — it goes back for re-approval (D-9, lane 1).</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/request-change")]
    public async Task<IActionResult> RequestChange(Guid id, [FromBody] RequestStaffTravelChangeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out _) is { } contextError) return contextError;

        await _service.RequestChangeAsync(id, dto.Reason);
        return Ok(new { message = "The trip is back for revision and will be approved again." });
    }

    /// <summary>Withdraw a submitted request from approval, back to Draft (lane 1).</summary>
    /// <remarks>
    /// The traveller's or whoever raised it — the service checks; with a workflow instance the engine
    /// also insists on the login that submitted it. The reason is optional.
    /// </remarks>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/recall")]
    public async Task<IActionResult> Recall(
        Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RecallStaffTravelRequestDto? dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out _) is { } contextError) return contextError;

        await _service.RecallAsync(id, dto?.Reason);
        return Ok(new { message = "Travel request recalled to draft." });
    }

    /// <summary>Close a completed trip once every claim and advance on it is finished (D-6, lane 1).</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id)
    {
        if (TryGetWriteContext(out _, out _) is { } contextError) return contextError;

        await _service.CloseAsync(id);
        return Ok(new { message = "Travel request closed." });
    }

    // =========================================================================
    // COMMENTS
    // =========================================================================

    // Reading the comments is the approver's door's (StaffTravelApprovalsController, lane 2).

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

    /// <summary>Edit a comment — its author's, or a travel administrator's (lane 1, finding A10).</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("comments/{commentId:guid}")]
    public async Task<ActionResult<StaffTravelRequestCommentDto>> UpdateComment(Guid commentId, [FromBody] UpdateStaffTravelRequestCommentDto dto)
    {
        if (commentId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateCommentAsync(dto, userId, await CallerIsTravelAdminAsync()));
    }

    /// <summary>
    /// Delete a comment — its author's, or a travel administrator's (lane 1, finding A10). It was
    /// administrators only, so an officer could not take back a comment they had just posted, and any
    /// officer could edit a colleague's.
    /// </summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId)
    {
        await _service.DeleteCommentAsync(commentId, await CallerIsTravelAdminAsync());
        return NoContent();
    }

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    // Listing and downloading the attachments are the approver's door's (StaffTravelApprovalsController,
    // lane 2).

    /// <summary>
    /// Attaches a document to a travel request through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// <para>Multipart, not JSON. The previous endpoint took a caller-supplied <c>FileUrl</c>,
    /// which let anyone with travel write access point an attachment at arbitrary bytes on disk —
    /// including another tenant's. That is the same path-injection sink medical exam documents and
    /// claim receipts were both fixed for, and a travel attachment is a passport scan or a visa
    /// letter, so it is squarely in scope.</para>
    ///
    /// <para>The file is scanned, registered in the DMS and stored outside the web root; the row
    /// keeps the three DMS ids and an empty <c>FileUrl</c>. Read it back through
    /// <c>attachments/{id}/download</c>.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{requestId:guid}/attachments")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> AddAttachment(
        Guid requestId,
        IFormFile? file,
        [FromForm] TravelAttachmentType attachmentType = TravelAttachmentType.Other,
        [FromForm] string? description = null,
        CancellationToken ct = default)
    {
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out _,
                "Attaching a document to a travel request") is { } contextError) return contextError;

        // Establish the caller may touch the parent BEFORE storing anything — neither the gate nor
        // the DMS performs an entitlement check.
        await _service.GetByIdAsync(requestId, ct);

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, CurrentUser, _logger, file,
            sourceEntityType: nameof(Core.Entities.HR.StaffTravel.StaffTravelRequest),
            sourceRecordId: requestId,
            sourceLabel: "Staff travel attachment",
            documentType: "StaffTravelAttachment",
            description: description,
            persist: (uploadedById, document) => _service.AddAttachmentAsync(
                new CreateStaffTravelRequestAttachmentDto
                {
                    StaffTravelRequestId = requestId,
                    FileName = document.OriginalFileName,
                    FileSizeBytes = document.FileSize,
                    MimeType = document.ContentType,
                    AttachmentType = attachmentType,
                    FileUploadRecordId = document.FileUploadRecordId,
                    DocumentRecordId = document.DocumentRecordId,
                    DocumentVersionId = document.DocumentVersionId,
                },
                tenantId, userId, uploadedById, ct),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrStaffTravelAttachments);
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

    /// <summary>
    /// Put an existing request on the group (lane 1, finding T-30 — the only door used to create a new
    /// one). A draft or a returned request; it takes the group's destination and dates.
    /// </summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("groups/{groupId:guid}/requests/{requestId:guid}")]
    public async Task<ActionResult<StaffGroupTravelDto>> LinkGroupParticipant(Guid groupId, Guid requestId)
        => Ok(await _service.LinkGroupParticipantAsync(groupId, requestId));

    /// <summary>Open the group to travellers, or reopen a closed one (lane 1 — the status is no longer the PUT's).</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("groups/{id:guid}/open")]
    public async Task<ActionResult<StaffGroupTravelDto>> OpenGroup(Guid id)
        => Ok(await _service.OpenGroupTravelAsync(id));

    /// <summary>Close the group to new travellers; their trips carry on.</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("groups/{id:guid}/close")]
    public async Task<ActionResult<StaffGroupTravelDto>> CloseGroup(Guid id)
        => Ok(await _service.CloseGroupTravelAsync(id));

    /// <summary>Call the group off — once none of its travellers has a trip still going ahead.</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("groups/{id:guid}/cancel")]
    public async Task<ActionResult<StaffGroupTravelDto>> CancelGroup(Guid id)
        => Ok(await _service.CancelGroupTravelAsync(id));
}
