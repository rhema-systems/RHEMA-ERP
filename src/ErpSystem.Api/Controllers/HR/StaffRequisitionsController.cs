using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;
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
/// Staff (headcount) requisitions.
///
/// <para><b>Approval authority comes from the published <c>StaffRequisition</c> workflow
/// definition, not from a role attribute.</b> <c>Approve</c> and <c>Reject</c> therefore carry no
/// <c>[Authorize(Roles = …)]</c> — the service asks the engine whether the caller is an approver
/// for the current step.</para>
///
/// <para>⚠ <b>This used to claim that submit and approve are "inoperable by design" until a
/// definition is published. They were not inoperable — submit auto-approved</b> (G-4.1, corrected
/// 2026-09-15). With no definition the engine's "approval is not configured" signal reached the
/// status adapter as <c>Approved</c>. The service now asks
/// <c>HasActiveApprovalWorkflowAsync</c> first and lands an unconfigured submission at
/// <c>Submitted</c>, where <c>ApproveAsync</c> — and the segregation-of-duties rule it carries —
/// can do its job. Authority on that path falls to <c>HR.Recruitment.Admin</c>; see
/// <c>RecruitmentApprovalAuthority</c>.</para>
///
/// <para>Everything that is not an approval decision is gated here: retiring, parking, fulfilling
/// and costing a requisition are HR's, while raising, editing, submitting and recalling one belong
/// to the manager who wants the head — so those check the requester rather than a role.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class StaffRequisitionsController : ControllerBase
{
    private readonly IStaffRequisitionService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<StaffRequisitionsController> _logger;
    private readonly IAuthorizationService _authorization;

    public StaffRequisitionsController(
        IStaffRequisitionService service,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ILogger<StaffRequisitionsController> logger,
        IAuthorizationService authorization)
    {
        _service = service;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorageService = fileStorageService;
        _db = db;
        _logger = logger;
        _authorization = authorization;
    }

    /// <summary>Self-or-permission (W3), as on LeavesController — see the remarks there.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUser.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>
    /// The desk, or the employee who raised the requisition. Used on the endpoints that belong to
    /// the requester rather than to a role — editing a draft, sending it for approval, taking it
    /// back. (W3 slice 9: the desk arm moved from the HR role to the Write permission.)
    /// </summary>
    private async Task<bool> CanActAsRequesterAsync(Guid requisitionId, CancellationToken ct)
    {
        var requisition = await _service.GetByIdAsync(requisitionId, ct);
        if (requisition == null) return false;
        return await SelfOrPolicyAsync(requisition.RequestedById, HrPermissions.RecruitmentWritePolicy);
    }

    /// <summary>The requester's read arm of the same rule — own requisition, or the desk read.</summary>
    private async Task<bool> CanReadRequisitionAsync(Guid requisitionId, CancellationToken ct)
    {
        var requisition = await _service.GetByIdAsync(requisitionId, ct);
        if (requisition == null) return false;
        return await SelfOrPolicyAsync(requisition.RequestedById, HrPermissions.RecruitmentReadPolicy);
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    #region Queries

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffRequisitionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result == null) return NotFound();
        // W3: the requester tracks their own requisition; anyone else needs the desk read.
        if (!await SelfOrPolicyAsync(result.RequestedById, HrPermissions.RecruitmentReadPolicy))
            return Forbid();
        return Ok(result);
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<StaffRequisitionDetailDto>> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetDetailAsync(id, ct);
        if (result == null) return NotFound(new { message = "Requisition not found." });
        if (!await SelfOrPolicyAsync(result.RequestedById, HrPermissions.RecruitmentReadPolicy))
            return Forbid();
        return Ok(result);
    }

    [HttpGet("{id:guid}/budget-check")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<RequisitionBudgetCheckDto>> CheckBudget(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.CheckBudgetAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// The budget and establishment check for a requisition that is still being typed (round 2b,
    /// R5). Any internal user: the requester sees the consequence before saving, not after.
    /// </summary>
    [HttpPost("budget-check/preview")]
    public async Task<ActionResult<RequisitionBudgetCheckDto>> PreviewBudgetCheck([FromBody] RequisitionBudgetCheckPreviewDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.PreviewBudgetCheckAsync(dto, ct));
    }

    /// <summary>Raises a Draft requisition drawing down what an approved budget line has left (round 2b, R5).</summary>
    [HttpPost("from-budget-line/{lineId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<StaffRequisitionDto>> CreateFromBudgetLine(Guid lineId, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        var created = await _service.CreateFromBudgetLineAsync(lineId, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("number/{requisitionNumber}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<StaffRequisitionDto>> GetByRequisitionNumber(string requisitionNumber, CancellationToken ct)
    {
        var result = await _service.GetByRequisitionNumberAsync(requisitionNumber, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("summary")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<StaffRequisitionStatusSummaryDto>> GetStatusSummary(CancellationToken ct)
        => Ok(await _service.GetStatusSummaryAsync(ct));

    [HttpGet]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffRequisitionSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetByOrganizationUnit(Guid organizationUnitId, CancellationToken ct)
        => Ok(await _service.GetByOrganizationUnitAsync(organizationUnitId, ct));

    [HttpGet("location/{locationId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetByLocation(Guid locationId, CancellationToken ct)
        => Ok(await _service.GetByLocationAsync(locationId, ct));

    [HttpGet("position/{positionId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetByPosition(Guid positionId, CancellationToken ct)
        => Ok(await _service.GetByPositionAsync(positionId, ct));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetByStatus(StaffRequisitionStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("type/{type}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetByType(StaffRequisitionType type, CancellationToken ct)
        => Ok(await _service.GetByTypeAsync(type, ct));

    [HttpGet("requested-by/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetByRequestedBy(Guid employeeId, CancellationToken ct)
    {
        // W3: a manager lists their own requisitions; anyone else's need the desk read.
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.RecruitmentReadPolicy))
            return Forbid();
        return Ok(await _service.GetByRequestedByAsync(employeeId, ct));
    }

    [HttpGet("pending-review")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetPendingReview(CancellationToken ct)
        => Ok(await _service.GetPendingReviewAsync(ct));

    [HttpGet("open")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetOpen(CancellationToken ct)
        => Ok(await _service.GetOpenRequisitionsAsync(ct));

    [HttpGet("overdue")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetOverdue(CancellationToken ct)
        => Ok(await _service.GetOverdueAsync(ct));

    [HttpGet("vacancy/{jobVacancyId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetByJobVacancy(Guid jobVacancyId, CancellationToken ct)
        => Ok(await _service.GetByJobVacancyAsync(jobVacancyId, ct));

    [HttpGet("upcoming-start")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionSummaryDto>>> GetUpcomingStartDate(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetUpcomingStartDateAsync(daysAhead, ct));

    #endregion

    // =========================================================================
    // CRUD
    // =========================================================================

    #region CRUD

    [HttpPost]
    public async Task<ActionResult<StaffRequisitionDto>> Create([FromBody] CreateStaffRequisitionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffRequisitionDto>> Update(Guid id, [FromBody] UpdateStaffRequisitionDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        if (!await CanActAsRequesterAsync(id, ct))
            return Forbid();

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    #endregion

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    #region Workflow

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitStaffRequisitionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RequisitionId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        if (!await CanActAsRequesterAsync(id, ct))
            return Forbid();

        await _service.SubmitAsync(dto, employeeId.Value, ct);

        // Return the requisition rather than a fixed message: with a multi-step definition the
        // engine decides where submission lands, and the caller needs the status it actually
        // reached, not this endpoint's guess at it.
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    /// <summary>
    /// Withdraws a requisition awaiting approval back to Draft. The service refuses unless the
    /// caller is the person who raised it.
    /// </summary>
    [HttpPost("{id:guid}/recall")]
    public async Task<IActionResult> Recall(Guid id, [FromBody] RecallStaffRequisitionDto? dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecallAsync(id, dto?.Reason, employeeId.Value, ct);
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    /// <summary>
    /// No role gate: authority for this step comes from the published workflow definition, and the
    /// service refuses anyone the engine does not name as an approver.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveStaffRequisitionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RequisitionId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ApproveAsync(dto, employeeId.Value, ct);

        // An approval step is not necessarily the last one — a two-approver definition leaves the
        // requisition Submitted after the first sign-off. Return what it became.
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    /// <summary>
    /// No role gate, for the same reason as <see cref="Approve"/>.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectStaffRequisitionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RequisitionId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RejectAsync(dto, employeeId.Value, ct);
        return Ok(await _service.GetByIdAsync(id, ct));
    }

    [HttpPost("{id:guid}/hold")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> PutOnHold(Guid id, [FromBody] HoldStaffRequisitionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RequisitionId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.PutOnHoldAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Requisition put on hold." });
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelStaffRequisitionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RequisitionId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CancelAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Requisition cancelled." });
    }

    [HttpPost("{id:guid}/fulfill")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> Fulfill(Guid id, [FromBody] FulfillStaffRequisitionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RequisitionId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.FulfillAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Requisition fulfillment recorded." });
    }

    [HttpPost("{id:guid}/link-vacancy")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> LinkToVacancy(Guid id, [FromBody] LinkStaffRequisitionToVacancyDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.RequisitionId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.LinkToVacancyAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Requisition linked to vacancy." });
    }

    #endregion

    // =========================================================================
    // COSTS
    // =========================================================================

    #region Costs

    [HttpPost("{id:guid}/costs")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<StaffRequisitionCostDto>> AddCost(Guid id, [FromBody] CreateStaffRequisitionCostDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.RequisitionId = id;
        var created = await _service.AddCostAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpGet("{id:guid}/costs")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionCostDto>>> GetCosts(Guid id, CancellationToken ct)
        => Ok(await _service.GetCostsAsync(id, ct));

    [HttpGet("{id:guid}/costs/total")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<decimal>> GetTotalCost(Guid id, [FromQuery] StaffRequisitionCostStatus? status, CancellationToken ct)
        => Ok(await _service.GetTotalCostAsync(id, status, ct));

    /// <summary>HR approves a recorded cost (round 2b, R7). Not the person who recorded it.</summary>
    [HttpPost("costs/{costId:guid}/approve")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<StaffRequisitionCostDto>> ApproveCost(Guid costId, [FromBody] DecideStaffRequisitionCostDto? dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        return Ok(await _service.DecideCostAsync(costId, approve: true, dto?.Note, employeeId.Value, ct));
    }

    /// <summary>HR rejects a recorded cost (round 2b, R7).</summary>
    [HttpPost("costs/{costId:guid}/reject")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<StaffRequisitionCostDto>> RejectCost(Guid costId, [FromBody] DecideStaffRequisitionCostDto? dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        return Ok(await _service.DecideCostAsync(costId, approve: false, dto?.Note, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/costs/category/{category}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionCostDto>>> GetCostsByCategory(Guid id, StaffRequisitionCostCategory category, CancellationToken ct)
        => Ok(await _service.GetCostsByCategoryAsync(id, category, ct));

    [HttpPut("costs/{costId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<StaffRequisitionCostDto>> UpdateCost(Guid costId, [FromBody] UpdateStaffRequisitionCostDto dto, CancellationToken ct)
    {
        if (costId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateCostAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("costs/{costId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteCost(Guid costId, CancellationToken ct)
    {
        await _service.DeleteCostAsync(costId, ct);
        return NoContent();
    }

    #endregion

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    #region Attachments

    /// <summary>
    /// Attaches a document to a requisition through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// Replaces a JSON endpoint that accepted a caller-supplied <c>filePath</c>: it stored no file,
    /// scanned nothing, and recorded whatever path was posted. Same shape as the five
    /// appraisal-domain attachment paths fixed before it.
    ///
    /// <para>⚠ Requires a working ClamAV — <c>hr-recruitment-attachments</c> is in
    /// <c>SystemCleanScanRequired</c> and tenant policy cannot turn that off, so with no scanner the
    /// gate refuses with 422 before the row is ever written.</para>
    /// </remarks>
    [HttpPost("{id:guid}/attachments")]
    [ProducesResponseType(typeof(StaffRequisitionAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        Guid id, IFormFile file, [FromForm] string? description, CancellationToken ct)
    {
        if (!await CanActAsRequesterAsync(id, ct))
            return Forbid();

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "StaffRequisition",
            sourceRecordId: id,
            sourceLabel: "Staff requisition attachment",
            documentType: "StaffRequisitionAttachment",
            description: description,
            persist: (uploadedById, document) => _service.AddAttachmentAsync(
                id, uploadedById, document.OriginalFileName, document.FileSize, description, ct,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrRecruitmentAttachments);
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<StaffRequisitionAttachmentDto>>> GetAttachments(Guid id, CancellationToken ct)
    {
        // W3: the requester sees their own requisition's attachments; anyone else needs the desk read.
        if (!await CanReadRequisitionAsync(id, ct)) return Forbid();
        return Ok(await _service.GetAttachmentsAsync(id, ct));
    }

    /// <summary>Streams a requisition attachment — the file lives outside the web root.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        // W3: previously tenant-scoped only — any internal user could pull any requisition's file.
        if (!await CanReadRequisitionAsync(id, ct)) return Forbid();

        var attachment = await _db.Set<StaffRequisitionAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.RequisitionId == id && a.TenantId == tenantId && !a.IsDeleted,
                ct);

        if (attachment is null)
            return NotFound(new { message = "Attachment not found" });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorageService, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FilePath,
            attachment.FileName, fallbackContentType: null,
            inline: false, cancellationToken: ct);
    }

    [HttpGet("attachments/uploader/{uploadedByUserId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionAttachmentDto>>> GetAttachmentsByUploader(Guid uploadedByUserId, CancellationToken ct)
        => Ok(await _service.GetAttachmentsByUploaderAsync(uploadedByUserId, ct));

    [HttpDelete("attachments/{attachmentId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId, CancellationToken ct)
    {
        await _service.DeleteAttachmentAsync(attachmentId, ct);
        return NoContent();
    }

    #endregion

    // =========================================================================
    // COMMENTS
    // =========================================================================

    #region Comments

    [HttpPost("{id:guid}/comments")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<StaffRequisitionCommentDto>> AddComment(Guid id, [FromBody] CreateStaffRequisitionCommentDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.RequisitionId = id;
        var created = await _service.AddCommentAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpGet("{id:guid}/comments")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionCommentDto>>> GetComments(Guid id, CancellationToken ct)
        => Ok(await _service.GetCommentsAsync(id, ct));

    [HttpGet("{id:guid}/comments/all")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionCommentDto>>> GetAllComments(Guid id, CancellationToken ct)
        => Ok(await _service.GetAllCommentsAsync(id, ct));

    [HttpGet("comments/author/{authorId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionCommentDto>>> GetCommentsByAuthor(Guid authorId, CancellationToken ct)
        => Ok(await _service.GetCommentsByAuthorAsync(authorId, ct));

    [HttpGet("comments/{parentCommentId:guid}/replies")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionCommentDto>>> GetCommentReplies(Guid parentCommentId, CancellationToken ct)
        => Ok(await _service.GetCommentRepliesAsync(parentCommentId, ct));

    [HttpPut("comments/{commentId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<StaffRequisitionCommentDto>> UpdateComment(Guid commentId, [FromBody] UpdateStaffRequisitionCommentDto dto, CancellationToken ct)
    {
        if (commentId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateCommentAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("comments/{commentId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct)
    {
        await _service.DeleteCommentAsync(commentId, ct);
        return NoContent();
    }

    #endregion

    // =========================================================================
    // HISTORY
    // =========================================================================

    #region History

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffRequisitionHistoryDto>>> GetHistory(Guid id, CancellationToken ct)
        => Ok(await _service.GetHistoryAsync(id, ct));

    [HttpGet("{id:guid}/history/latest")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<StaffRequisitionHistoryDto>> GetLatestHistory(Guid id, CancellationToken ct)
    {
        var result = await _service.GetLatestHistoryAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    #endregion
}
