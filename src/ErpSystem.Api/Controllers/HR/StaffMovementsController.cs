using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-movements")]
[Authorize]
public class StaffMovementsController : ControllerBase
{
    private readonly IStaffMovementService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffMovementsController(IStaffMovementService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffMovementSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] StaffMovementStatus? status = null,
        [FromQuery] StaffMovementType? type = null,
        [FromQuery] bool isPendingApproval = false)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, status, type, isPendingApproval));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Returns movement summary rows for many parent movement IDs (subtype list hydration).</summary>
    [HttpPost("summaries/by-ids")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetSummariesByIds(
        [FromBody] StaffMovementSummariesByIdsRequest request)
    {
        if (request.Ids is null || request.Ids.Count == 0)
            return Ok(Array.Empty<StaffMovementSummaryDto>());

        return Ok(await _service.GetSummariesByIdsAsync(request.Ids));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffMovementDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{movementNumber}")]
    public async Task<ActionResult<StaffMovementDto?>> GetByMovementNumber(string movementNumber)
        => Ok(await _service.GetByMovementNumberAsync(movementNumber));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeAsync(employeeId));

    [HttpGet("employee/{employeeId:guid}/latest")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetLatestForEmployee(Guid employeeId)
        => Ok(await _service.GetLatestMovementsForEmployeeAsync(employeeId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByStatus(StaffMovementStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByType(
        StaffMovementType type,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
        => Ok(await _service.GetByTypeAsync(type, from, to));

    [HttpGet("type/{type}/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByTypeAndStatus(
        StaffMovementType type, StaffMovementStatus status)
        => Ok(await _service.GetByTypeAndStatusAsync(type, status));

    [HttpGet("org-unit/current/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByCurrentOrgUnit(Guid organizationUnitId)
        => Ok(await _service.GetByCurrentOrganizationUnitAsync(organizationUnitId));

    [HttpGet("org-unit/new/{organizationUnitId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByNewOrgUnit(Guid organizationUnitId)
        => Ok(await _service.GetByNewOrganizationUnitAsync(organizationUnitId));

    [HttpGet("pending-approval")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetPendingApproval()
        => Ok(await _service.GetPendingApprovalAsync());

    [HttpGet("pending-employee-acceptance")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetPendingEmployeeAcceptance()
        => Ok(await _service.GetPendingEmployeeAcceptanceAsync());

    [HttpGet("pending-handover")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetPendingHandover()
        => Ok(await _service.GetPendingHandoverAsync());

    [HttpGet("temporary/active")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetActiveTemporary()
        => Ok(await _service.GetActiveTemporaryAssignmentsAsync());

    [HttpGet("temporary/expiring")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetExpiringTemporary(
        [FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringTemporaryAssignmentsAsync(daysAhead));

    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
        => Ok(await _service.GetByEffectiveDateRangeAsync(from, to));

    [HttpGet("requested-by/{requestedByEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetByRequestedBy(Guid requestedByEmployeeId)
        => Ok(await _service.GetByRequestedByAsync(requestedByEmployeeId));

    [HttpGet("succession-plan/{successionPlanId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementSummaryDto>>> GetBySuccessionPlan(Guid successionPlanId)
        => Ok(await _service.GetBySuccessionPlanAsync(successionPlanId));

    [HttpGet("dashboard")]
    public async Task<ActionResult<StaffMovementDashboardDto>> GetDashboard(
        [FromQuery] int?      filterYear = null,
        [FromQuery] DateTime? fromDate   = null,
        [FromQuery] DateTime? toDate     = null)
        => Ok(await _service.GetDashboardAsync(filterYear, fromDate, toDate));

    // =========================================================================
    // CRUD
    // =========================================================================

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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffMovementDto>> Update(Guid id, [FromBody] UpdateStaffMovementDto dto)
    {
        if (id != dto.Id)         return BadRequest("ID mismatch.");
        if (!ModelState.IsValid)  return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

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

    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> RecordEmployeeResponse(Guid id, [FromBody] RespondToStaffMovementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        dto.MovementId = id;
        await _service.RecordEmployeeResponseAsync(dto);
        return Ok(new { message = "Employee response recorded." });
    }

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

    [HttpGet("{id:guid}/approval-levels")]
    public async Task<ActionResult<IEnumerable<StaffMovementApprovalLevelDto>>> GetApprovalLevels(Guid id)
        => Ok(await _service.GetApprovalLevelsAsync(id));

    [HttpGet("{id:guid}/approval-levels/current-pending")]
    public async Task<ActionResult<StaffMovementApprovalLevelDto?>> GetCurrentPendingApprovalLevel(Guid id)
        => Ok(await _service.GetCurrentPendingApprovalLevelAsync(id));

    [HttpGet("{id:guid}/approval-levels/all-approved")]
    public async Task<ActionResult<bool>> AllLevelsApproved(Guid id)
        => Ok(await _service.AllLevelsApprovedAsync(id));

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

        dto.ApprovalLevelId = approvalLevelId;
        await _service.DelegateApprovalLevelAsync(dto);
        return Ok(new { message = "Approval level delegated." });
    }

    // =========================================================================
    // STATUS HISTORY
    // =========================================================================

    [HttpGet("{id:guid}/status-history")]
    public async Task<ActionResult<IEnumerable<StaffMovementStatusHistoryDto>>> GetStatusHistory(Guid id)
        => Ok(await _service.GetStatusHistoryAsync(id));

    [HttpGet("{id:guid}/status-history/latest")]
    public async Task<ActionResult<StaffMovementStatusHistoryDto?>> GetLatestStatus(Guid id)
        => Ok(await _service.GetLatestStatusAsync(id));

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<StaffMovementAttachmentDto>>> GetAttachments(Guid id)
        => Ok(await _service.GetAttachmentsAsync(id));

    [HttpGet("{id:guid}/attachments/type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffMovementAttachmentDto>>> GetAttachmentsByType(
        Guid id, StaffMovementAttachmentType type)
        => Ok(await _service.GetAttachmentsByTypeAsync(id, type));

    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult<StaffMovementAttachmentDto>> AddAttachment(
        Guid id, [FromBody] CreateStaffMovementAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.MovementId = id;
        var created = await _service.AddAttachmentAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetAttachments), new { id }, created);
    }

    [HttpPost("{id:guid}/attachments/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<StaffMovementAttachmentDto>> UploadAttachment(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] StaffMovementAttachmentType attachmentType = StaffMovementAttachmentType.Other,
        [FromForm] string? description = null)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".txt" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest($"File type '{ext}' is not permitted.");

        var storageDir = Path.Combine("uploads", "movements", id.ToString());
        Directory.CreateDirectory(storageDir);

        var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var filePath     = Path.Combine(storageDir, safeFileName);

        await using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream);

        var dto = new CreateStaffMovementAttachmentDto
        {
            MovementId  = id,
            FileName    = file.FileName,
            FilePath    = filePath,
            Type        = attachmentType,
            Description = description
        };

        var created = await _service.AddAttachmentAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetAttachments), new { id }, created);
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        await _service.DeleteAttachmentAsync(attachmentId);
        return NoContent();
    }

    // =========================================================================
    // CHECKLIST
    // =========================================================================

    [HttpGet("{id:guid}/checklist")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetChecklistItems(Guid id)
        => Ok(await _service.GetChecklistItemsAsync(id));

    [HttpGet("{id:guid}/checklist/pending")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetPendingChecklistItems(Guid id)
        => Ok(await _service.GetPendingChecklistItemsAsync(id));

    [HttpGet("{id:guid}/checklist/overdue")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetOverdueChecklistItemsForMovement(Guid id)
        => Ok(await _service.GetOverdueChecklistItemsAsync(id));

    [HttpGet("checklist/overdue")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetAllOverdueChecklistItems()
        => Ok(await _service.GetOverdueChecklistItemsAsync());

    [HttpGet("checklist/responsible/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffMovementChecklistItemDto>>> GetChecklistByResponsiblePerson(Guid employeeId)
        => Ok(await _service.GetChecklistItemsByResponsiblePersonAsync(employeeId));

    [HttpGet("{id:guid}/checklist/all-required-complete")]
    public async Task<ActionResult<bool>> AllRequiredItemsCompleted(Guid id)
        => Ok(await _service.AllRequiredItemsCompletedAsync(id));

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
        await _service.CompleteChecklistItemAsync(dto, employeeId.Value);
        return Ok(new { message = "Checklist item marked as complete." });
    }

    [HttpDelete("checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklistItem(Guid itemId)
    {
        await _service.DeleteChecklistItemAsync(itemId);
        return NoContent();
    }
}
