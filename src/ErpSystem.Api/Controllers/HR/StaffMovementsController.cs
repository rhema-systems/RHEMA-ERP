using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
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
/// Staff movement records — promotion, transfer, demotion, secondment, acting, lateral move,
/// redesignation — and their approval chain, status history, attachments and checklist.
///
/// Gated per action rather than at class level: a movement carries the subject's salary, grade and
/// reporting line, so the register and every workflow step are HR-only, but three actions are open to
/// the employee the movement is about — reading their own movements, reading their own outstanding
/// checklist tasks, and responding to a move proposed for them. Acceptance is testimony, so the
/// service refuses it from anyone but the subject, HR included.
/// </summary>
[ApiController]
[Route("api/staff-movements")]
[Authorize]
[MovementBusinessRules]
public class StaffMovementsController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IStaffMovementService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StaffMovementsController(
        IStaffMovementService service,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _service     = service;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db          = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Whether the caller may see and act on any movement, as opposed to only their own.
    /// "Admin" is included because the seeded administrator account is not in the HR role but is
    /// expected to be able to administer the module.
    /// </summary>
    private bool IsHr =>
        _currentUser.IsInRole(Constants.Roles.Hr) ||
        _currentUser.IsInRole(Constants.Roles.SuperAdmin) ||
        _currentUser.IsInRole("Admin");

    // =========================================================================
    // QUERIES
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffMovementSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] StaffMovementStatus? status = null,
        [FromQuery] StaffMovementType? type = null,
        [FromQuery] bool isPendingApproval = false)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, status, type, isPendingApproval));

    [Authorize(Roles = HrRoles)]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Returns movement summary rows for many parent movement IDs (subtype list hydration).</summary>
    [Authorize(Roles = HrRoles)]
    [HttpPost("summaries/by-ids")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetSummariesByIds(
        [FromBody] StaffMovementSummariesByIdsRequest request)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            return Ok(Array.Empty<StaffMovementSummaryDto>());

        return Ok(await _service.GetSummariesByIdsAsync(request.Ids));
    }

    /// <summary>
    /// Open to the movement's subject as well as HR — an employee can read the move proposed for
    /// them, which is the record they are asked to accept or decline.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffMovementDto>> GetById(Guid id)
    {
        var movement = await _service.GetByIdAsync(id);

        if (!IsHr && movement.EmployeeId != _currentUser.EmployeeId)
            return Forbid();

        return Ok(movement);
    }

    /// <summary>The caller's own movement history. The token supplies the employee — see GetByEmployee.</summary>
    [HttpGet("employee/me")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetMine()
    {
        if (_currentUser.EmployeeId is not Guid employeeId)
            return Forbid();

        return Ok(await _service.GetByEmployeeAsync(employeeId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpGet("number/{movementNumber}")]
    public async Task<ActionResult<StaffMovementDto?>> GetByMovementNumber(string movementNumber)
        => Ok(await _service.GetByMovementNumberAsync(movementNumber));

    [Authorize(Roles = HrRoles)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("employee/{employeeId:guid}/latest")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetLatestForEmployee(Guid employeeId)
        => Ok(await _service.GetLatestMovementsForEmployeeAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByStatus(StaffMovementStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [Authorize(Roles = HrRoles)]
    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByType(
        StaffMovementType type,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
        => Ok(await _service.GetByTypeAsync(type, from, to));

    [Authorize(Roles = HrRoles)]
    [HttpGet("type/{type}/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByTypeAndStatus(
        StaffMovementType type, StaffMovementStatus status)
        => Ok(await _service.GetByTypeAndStatusAsync(type, status));

    [Authorize(Roles = HrRoles)]
    [HttpGet("org-unit/current/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByCurrentOrgUnit(Guid organizationUnitId)
        => Ok(await _service.GetByCurrentOrganizationUnitAsync(organizationUnitId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("org-unit/new/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByNewOrgUnit(Guid organizationUnitId)
        => Ok(await _service.GetByNewOrganizationUnitAsync(organizationUnitId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("pending-approval")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetPendingApproval()
        => Ok(await _service.GetPendingApprovalAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("pending-employee-acceptance")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetPendingEmployeeAcceptance()
        => Ok(await _service.GetPendingEmployeeAcceptanceAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("pending-handover")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetPendingHandover()
        => Ok(await _service.GetPendingHandoverAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("temporary/active")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetActiveTemporary()
        => Ok(await _service.GetActiveTemporaryAssignmentsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("temporary/expiring")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetExpiringTemporary(
        [FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringTemporaryAssignmentsAsync(daysAhead));

    [Authorize(Roles = HrRoles)]
    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
        => Ok(await _service.GetByEffectiveDateRangeAsync(from, to));

    [Authorize(Roles = HrRoles)]
    [HttpGet("requested-by/{requestedByEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByRequestedBy(Guid requestedByEmployeeId)
        => Ok(await _service.GetByRequestedByAsync(requestedByEmployeeId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("succession-plan/{successionPlanId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetBySuccessionPlan(Guid successionPlanId)
        => Ok(await _service.GetBySuccessionPlanAsync(successionPlanId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("dashboard")]
    public async Task<ActionResult<StaffMovementDashboardDto>> GetDashboard(
        [FromQuery] int?      filterYear = null,
        [FromQuery] DateTime? fromDate   = null,
        [FromQuery] DateTime? toDate     = null)
        => Ok(await _service.GetDashboardAsync(filterYear, fromDate, toDate));

    // =========================================================================
    // CRUD
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpPost]
    public async Task<ActionResult<StaffMovementDto>> Create([FromBody] CreateStaffMovementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffMovementDto>> Update(Guid id, [FromBody] UpdateStaffMovementDto dto)
    {
        if (id != dto.Id)         return BadRequest("ID mismatch.");
        if (!ModelState.IsValid)  return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitStaffMovementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        await _service.SubmitAsync(dto, employeeId.Value);
        return Ok(new { message = "Movement submitted for approval." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/authorize")]
    public async Task<IActionResult> Authorize(Guid id, [FromBody] AuthorizeStaffMovementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        await _service.AuthorizeAsync(dto, employeeId.Value);
        return Ok(new { message = "Movement authorised." });
    }

    /// <summary>
    /// The subject's own acceptance or refusal of the move proposed for them. Open by design, and the
    /// service refuses any caller who is not the subject — HR included, since this is testimony.
    /// </summary>
    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> RecordEmployeeResponse(Guid id, [FromBody] RespondToStaffMovementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        await _service.RecordEmployeeResponseAsync(dto, employeeId.Value);
        return Ok(new { message = "Employee response recorded." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/handover")]
    public async Task<IActionResult> CompleteHandover(Guid id, [FromBody] CompleteHandoverDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        await _service.CompleteHandoverAsync(dto, employeeId.Value);
        return Ok(new { message = "Handover completed." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectStaffMovementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        await _service.RejectAsync(dto, employeeId.Value);
        return Ok(new { message = "Movement rejected." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelStaffMovementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        await _service.CancelAsync(dto, employeeId.Value);
        return Ok(new { message = "Movement cancelled." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> ProcessReturn(Guid id, [FromBody] ProcessReturnFromTemporaryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        await _service.ProcessReturnFromTemporaryAsync(dto, employeeId.Value);
        return Ok(new { message = "Return from temporary assignment processed." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/implement")]
    public async Task<IActionResult> Implement(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ImplementAsync(id, employeeId.Value);
        return Ok(new { message = "Movement marked as implemented." });
    }

    // =========================================================================
    // APPROVAL LEVELS
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/approval-levels")]
    public async Task<ActionResult<IEnumerable<StaffMovementApprovalLevelDto>>> GetApprovalLevels(Guid id)
        => Ok(await _service.GetApprovalLevelsAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/approval-levels/current-pending")]
    public async Task<ActionResult<StaffMovementApprovalLevelDto?>> GetCurrentPendingApprovalLevel(Guid id)
        => Ok(await _service.GetCurrentPendingApprovalLevelAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/approval-levels/all-approved")]
    public async Task<ActionResult<bool>> AllLevelsApproved(Guid id)
        => Ok(await _service.AllLevelsApprovedAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/approval-levels")]
    public async Task<ActionResult<StaffMovementApprovalLevelDto>> AddApprovalLevel(
        Guid id, [FromBody] CreateStaffMovementApprovalLevelDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        var created = await _service.AddApprovalLevelAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetApprovalLevels), new { id }, created);
    }

    [HttpPost("approval-levels/{approvalLevelId:guid}/action")]
    public async Task<IActionResult> ActionApprovalLevel(
        Guid approvalLevelId, [FromBody] ActionApprovalLevelDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ApprovalLevelId = approvalLevelId;
        await _service.ActionApprovalLevelAsync(dto, employeeId.Value);
        return Ok(new { message = "Approval level actioned." });
    }

    [HttpPost("approval-levels/{approvalLevelId:guid}/delegate")]
    public async Task<IActionResult> DelegateApprovalLevel(
        Guid approvalLevelId, [FromBody] DelegateApprovalLevelDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ApprovalLevelId = approvalLevelId;
        await _service.DelegateApprovalLevelAsync(dto, employeeId.Value);
        return Ok(new { message = "Approval level delegated." });
    }

    // =========================================================================
    // STATUS HISTORY
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/status-history")]
    public async Task<ActionResult<IEnumerable<StaffMovementStatusHistoryDto>>> GetStatusHistory(Guid id)
        => Ok(await _service.GetStatusHistoryAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/status-history/latest")]
    public async Task<ActionResult<StaffMovementStatusHistoryDto?>> GetLatestStatus(Guid id)
        => Ok(await _service.GetLatestStatusAsync(id));

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<StaffMovementAttachmentDto>>> GetAttachments(Guid id)
        => Ok(await _service.GetAttachmentsAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/attachments/type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffMovementAttachmentDto>>> GetAttachmentsByType(
        Guid id, StaffMovementAttachmentType type)
        => Ok(await _service.GetAttachmentsByTypeAsync(id, type));

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult<StaffMovementAttachmentDto>> AddAttachment(
        Guid id, [FromBody] CreateStaffMovementAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // A caller-supplied path would let anyone point an attachment row at arbitrary bytes
        // on disk, including another tenant's. Files arrive through the upload endpoint below.
        if (!string.IsNullOrWhiteSpace(dto.FilePath) ||
            dto.FileUploadRecordId.HasValue ||
            dto.DocumentRecordId.HasValue ||
            dto.DocumentVersionId.HasValue)
        {
            return BadRequest(
                "File locations cannot be supplied directly. " +
                "Use POST {id}/attachments/upload to attach a file.");
        }

        dto.MovementId = id;
        var created = await _service.AddAttachmentAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetAttachments), new { id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/attachments/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<StaffMovementAttachmentDto>> UploadAttachment(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] StaffMovementAttachmentType attachmentType = StaffMovementAttachmentType.Other,
        [FromForm] string? description = null,
        CancellationToken ct = default)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        if (!Guid.TryParse(_currentUser.UserId, out var actorUserId))
            return BadRequest("User context could not be resolved.");

        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        // Extension, MIME, size, quota and malware checks all live in the shared upload gate;
        // this used to write the file straight to disk with System.IO.File.Create.
        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId.Value,
                ActorUserId = actorUserId,
                ActorName = _currentUser.UserName,
                Category = ControlledFileUploadCategories.HrStaffMovementAttachments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Staff movement attachment",
                    SourceEntityType = "StaffMovement",
                    SourceRecordId = id,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = attachmentType.ToString(),
                    ChangeSummary = description
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var dto = new CreateStaffMovementAttachmentDto
            {
                MovementId  = id,
                FileName    = document.OriginalFileName,
                FilePath    = string.Empty,
                FileUploadRecordId = document.FileUploadRecordId,
                DocumentRecordId   = document.DocumentRecordId,
                DocumentVersionId  = document.DocumentVersionId,
                Type        = attachmentType,
                Description = description
            };

            var created = await _service.AddAttachmentAsync(dto, tenantId.Value, employeeId.Value);
            return CreatedAtAction(nameof(GetAttachments), new { id }, created);
        }
        catch
        {
            await _hrDocuments.RollbackAsync(document, tenantId.Value, actorUserId, ct);
            throw;
        }
    }

    /// <summary>
    /// Streams a staff movement attachment to a caller entitled to see it.
    /// </summary>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var attachment = await _db.Set<Core.Entities.HR.PromotionTransfer.StaffMovementAttachment>()
            .AsNoTracking()
            .Include(item => item.Movement)
            .SingleOrDefaultAsync(
                item => item.Id == attachmentId && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (attachment is null)
            return NotFound();

        var isSubject = _currentUser.EmployeeId is Guid employeeId &&
                        attachment.Movement.EmployeeId == employeeId;
        if (!isSubject && !IsHr)
            return Forbid();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FilePath,
            attachment.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        await _service.DeleteAttachmentAsync(attachmentId);
        return NoContent();
    }

    // =========================================================================
    // CHECKLIST
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/checklist")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetChecklistItems(Guid id)
        => Ok(await _service.GetChecklistItemsAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/checklist/pending")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetPendingChecklistItems(Guid id)
        => Ok(await _service.GetPendingChecklistItemsAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/checklist/overdue")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetOverdueChecklistItemsForMovement(Guid id)
        => Ok(await _service.GetOverdueChecklistItemsAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("checklist/overdue")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetAllOverdueChecklistItems()
        => Ok(await _service.GetOverdueChecklistItemsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("checklist/responsible/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetChecklistByResponsiblePerson(Guid employeeId)
        => Ok(await _service.GetChecklistItemsByResponsiblePersonAsync(employeeId));

    /// <summary>
    /// The caller's own outstanding movement tasks. Without this the open completion endpoint below
    /// would be unusable by the people who owe the tasks — they have no way to learn their own id.
    /// </summary>
    [HttpGet("checklist/mine")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetMyChecklistItems()
    {
        if (_currentUser.EmployeeId is not Guid employeeId)
            return Forbid();

        return Ok(await _service.GetChecklistItemsByResponsiblePersonAsync(employeeId));
    }

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}/checklist/all-required-complete")]
    public async Task<ActionResult<bool>> AllRequiredItemsCompleted(Guid id)
        => Ok(await _service.AllRequiredItemsCompletedAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/checklist")]
    public async Task<ActionResult<StaffMovementChecklistItemDto>> AddChecklistItem(
        Guid id, [FromBody] CreateStaffMovementChecklistItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        var created = await _service.AddChecklistItemAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetChecklistItems), new { id }, created);
    }

    [HttpPost("checklist/{itemId:guid}/complete")]
    public async Task<IActionResult> CompleteChecklistItem(Guid itemId, [FromBody] CompleteChecklistItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ItemId = itemId;
        await _service.CompleteChecklistItemAsync(dto, employeeId.Value, IsHr);
        return Ok(new { message = "Checklist item marked as complete." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklistItem(Guid itemId)
    {
        await _service.DeleteChecklistItemAsync(itemId);
        return NoContent();
    }
}
