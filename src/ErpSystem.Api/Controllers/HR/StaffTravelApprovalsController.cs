using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The approver's door to a travel request: the queue, the request and what is on it, and the three
/// decisions (staff travel final closure, lane 2 — decision D-7, finding O-1).
/// </summary>
/// <remarks>
/// <para><b>Why these actions are not on <see cref="StaffTravelRequestsController"/>.</b> That controller
/// carries <c>HR.Travel.Read</c> at class level, and a class-level policy is ANDed with an action's — so a
/// line manager, who holds no travel permission, could neither open a request nor approve it, though the
/// seeded route named the Manager role first and <c>/me/inbox</c> linked them to it. These actions share
/// its route and sit behind <c>InternalOnly</c> instead, and each checks the door itself.</para>
///
/// <para><b>The door</b> is leave's (<c>LeavesController.CanReadRequestAsync</c>): the travel read
/// permission, OR the traveller's line authority, OR the person the request is waiting for now. The
/// decisions are not permission-gated at all: the travel service refuses everyone but whoever may decide
/// at the stage the request is on — the traveller's line authority at the line manager's stage (the
/// travel desk there only when no line authority can sign in), the route's approvers after.</para>
/// </remarks>
[ApiController]
[Route("api/staff-travel/requests")]
[StaffTravelBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class StaffTravelApprovalsController : HrControllerBase
{
    private readonly IStaffTravelRequestService _service;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly IAuthorizationService _authorization;

    public StaffTravelApprovalsController(
        IStaffTravelRequestService service,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        IAuthorizationService authorization,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _authorization = authorization;
    }

    /// <summary>The caller satisfies the policy — database grants and the HR role fallback both count.</summary>
    private async Task<bool> HoldsAsync(string policy)
        => (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

    /// <summary>
    /// Whether the caller is the travel desk (<c>HR.Travel.Write</c>) — who decides the line manager's stage
    /// when the traveller has no line authority who can sign in. Evaluated here, never read from a body.
    /// </summary>
    private Task<bool> CallerIsTravelDeskAsync() => HoldsAsync(HrPermissions.TravelWritePolicy);

    /// <summary>Travel read, OR the traveller's line authority, OR the person the request waits for now.</summary>
    private async Task<bool> CanOpenAsync(Guid requestId, CancellationToken ct)
        => await HoldsAsync(HrPermissions.TravelReadPolicy)
           || await _service.CanOpenAsApproverAsync(requestId, await CallerIsTravelDeskAsync(), ct);

    // =========================================================================
    // THE QUEUE AND THE DOOR
    // =========================================================================

    /// <summary>Travel requests waiting for YOUR decision.</summary>
    /// <remarks>
    /// Asked of the workflow engine and then of the line rule, request by request — exactly what the
    /// decision verbs will accept, so no row refuses on click. No approver parameter: the answer is only
    /// ever about the caller.
    /// </remarks>
    [HttpGet("my-approvals")]
    public async Task<ActionResult<PagedResult<StaffTravelApprovalQueueItemDto>>> GetMyApprovals(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _service.GetMyPendingApprovalsAsync(pageNumber, pageSize, await CallerIsTravelDeskAsync(), ct));

    /// <summary>What the caller may decide on this request, at which stage, and as whom.</summary>
    [HttpGet("{id:guid}/viewer-actions")]
    public async Task<ActionResult<StaffTravelViewerActionsDto>> GetViewerActions(Guid id, CancellationToken ct = default)
    {
        if (!await CanOpenAsync(id, ct)) return Forbid();
        return Ok(await _service.GetViewerActionsAsync(id, await CallerIsTravelDeskAsync(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffTravelRequestDto>> GetById(Guid id, CancellationToken ct = default)
    {
        if (!await CanOpenAsync(id, ct)) return Forbid();
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    /// <summary>
    /// The destination alerts in force over the trip (lane 5, T-45) — on this door because the approver deciding the trip
    /// needs them and may hold no travel permission; the alerts register itself stays the desk's.
    /// </summary>
    [HttpGet("{id:guid}/destination-alerts")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertSummaryDto>>> GetDestinationAlerts(Guid id, CancellationToken ct = default)
    {
        if (!await CanOpenAsync(id, ct)) return Forbid();
        return Ok(await _service.GetDestinationAlertsAsync(id, ct));
    }

    /// <summary>The request's comments — internal notes included: the reader is staff deciding or managing it.</summary>
    [HttpGet("{requestId:guid}/comments")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestCommentDto>>> GetComments(Guid requestId, CancellationToken ct = default)
    {
        if (!await CanOpenAsync(requestId, ct)) return Forbid();
        return Ok(await _service.GetCommentsAsync(requestId, ct));
    }

    [HttpGet("{requestId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<StaffTravelRequestAttachmentDto>>> GetAttachments(Guid requestId, CancellationToken ct = default)
    {
        if (!await CanOpenAsync(requestId, ct)) return Forbid();
        return Ok(await _service.GetAttachmentsAsync(requestId, ct));
    }

    /// <summary>Streams a travel attachment back, byte-for-byte — to whoever may open its request.</summary>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct = default)
    {
        if (CurrentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var attachment = await _db.Set<Core.Entities.HR.StaffTravel.StaffTravelRequestAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.TenantId == tenantId && !a.IsDeleted, ct);
        if (attachment is null) return NotFound();
        if (!await CanOpenAsync(attachment.StaffTravelRequestId, ct)) return Forbid();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FileUrl,
            attachment.FileName, fallbackContentType: attachment.MimeType,
            inline: false, ct);
    }

    // =========================================================================
    // THE DECISIONS — not permission-gated: the service refuses all but the stage's decider
    // =========================================================================

    /// <summary>Approve the stage the request is on.</summary>
    /// <remarks>
    /// At the line manager's stage the request goes on to HR; at HR's it is approved, with the budget HR
    /// sets (the estimate when none is sent). A budget sent at the line manager's stage is refused.
    /// </remarks>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveStaffTravelRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out _) is { } contextError) return contextError;

        dto.RequestId = id;
        await _service.ApproveAsync(dto, await CallerIsTravelDeskAsync());

        var now = await _service.GetByIdAsync(id);
        return Ok(new
        {
            message = now.Status == Core.Enums.StaffTravelRequestStatus.Approved
                ? "Travel request approved."
                : "Approved at this stage — it goes on to the next approver.",
            status = now.Status.ToString(),
        });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromQuery] string? reason = null)
    {
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        await _service.RejectAsync(id, userId, reason, await CallerIsTravelDeskAsync());
        return Ok(new { message = "Travel request rejected." });
    }

    /// <summary>Send a submitted request back to its requester, saying what to change (D-6).</summary>
    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> ReturnForRevision(Guid id, [FromBody] ReturnStaffTravelRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out _) is { } contextError) return contextError;

        await _service.ReturnForRevisionAsync(id, dto.Reason, await CallerIsTravelDeskAsync());
        return Ok(new { message = "Travel request returned for revision." });
    }
}
