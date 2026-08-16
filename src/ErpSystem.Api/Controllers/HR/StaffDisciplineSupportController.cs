using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
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
/// Manages the support entities of a disciplinary case:
/// Action Steps, Witnesses, Documents, Notes, Notifications, and Legal Reviews.
/// All routes are nested under <c>api/discipline</c>.
///
/// Gated per action. Everything here is HR-only — case notes, witness statements, legal reviews and
/// the evidence file are the investigation's working papers — except acknowledging a notice, which
/// only the subject can do. Stacked <c>[Authorize]</c> attributes are ANDed, so a class-level role
/// gate would make that acknowledgement impossible for the one person entitled to give it.
/// </summary>
[ApiController]
[Route("api/discipline")]
[Authorize]
[DisciplineBusinessRules]
public class StaffDisciplineSupportController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IStaffDisciplineActionStepService _actionStepService;
    private readonly IStaffDisciplineWitnessService _witnessService;
    private readonly IStaffDisciplineDocumentService _documentService;
    private readonly IStaffDisciplineNoteService _noteService;
    private readonly IStaffDisciplineNotificationService _notificationService;
    private readonly IStaffDisciplineLegalReviewService _legalReviewService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StaffDisciplineSupportController(
        IStaffDisciplineActionStepService actionStepService,
        IStaffDisciplineWitnessService witnessService,
        IStaffDisciplineDocumentService documentService,
        IStaffDisciplineNoteService noteService,
        IStaffDisciplineNotificationService notificationService,
        IStaffDisciplineLegalReviewService legalReviewService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _actionStepService = actionStepService;
        _witnessService = witnessService;
        _documentService = documentService;
        _noteService = noteService;
        _notificationService = notificationService;
        _legalReviewService = legalReviewService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// True when the caller holds a role permitted to read disciplinary evidence.
    /// </summary>
    private bool CallerIsDisciplineReader()
        => _currentUser.IsInRole("HR") ||
           _currentUser.IsInRole("Admin") ||
           _currentUser.IsInRole("SuperAdmin");

    // =========================================================================
    // ACTION STEPS
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/action-steps")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineActionStepDto>>> GetActionSteps(Guid caseId)
        => Ok(await _actionStepService.GetByCaseIdAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/action-steps/pending")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineActionStepDto>>> GetPendingActionSteps(Guid caseId)
        => Ok(await _actionStepService.GetPendingStepsAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("action-steps/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineActionStepDto?>> GetActionStepById(Guid id)
        => Ok(await _actionStepService.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("action-steps/overdue")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineActionStepDto>>> GetOverdueActionSteps()
        => Ok(await _actionStepService.GetOverdueStepsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("action-steps/by-actioned-by/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineActionStepDto>>> GetActionStepsByActionedBy(Guid employeeId)
        => Ok(await _actionStepService.GetByActionedByAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpPost("cases/{caseId:guid}/action-steps/initialise")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineActionStepDto>>> InitialiseActionSteps(Guid caseId)
    {
        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var steps = await _actionStepService.InitialiseFromOffenseProceduresAsync(caseId, tenantId.Value, employeeId.Value);
        return Ok(steps);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("action-steps/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineActionStepDto>> UpdateActionStep(
        Guid id, [FromBody] UpdateActionStepDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.StepId = id;
        return Ok(await _actionStepService.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("action-steps/{id:guid}/complete")]
    public async Task<IActionResult> CompleteActionStep(Guid id, [FromBody] string notes)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        await _actionStepService.CompleteStepAsync(id, notes, employeeId.Value);
        return Ok(new { message = "Action step completed." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("action-steps/{id:guid}/skip")]
    public async Task<IActionResult> SkipActionStep(Guid id, [FromBody] string reason)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        await _actionStepService.SkipStepAsync(id, reason, employeeId.Value);
        return Ok(new { message = "Action step skipped." });
    }

    // =========================================================================
    // WITNESSES
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/witnesses")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineWitnessSummaryDto>>> GetWitnesses(Guid caseId)
        => Ok(await _witnessService.GetByCaseIdAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/witnesses/without-statement")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineWitnessSummaryDto>>> GetWitnessesWithoutStatement(Guid caseId)
        => Ok(await _witnessService.GetWithoutStatementAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("witnesses/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineWitnessDto?>> GetWitnessById(Guid id)
        => Ok(await _witnessService.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("witnesses/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineWitnessSummaryDto>>> GetWitnessesByEmployee(Guid employeeId)
        => Ok(await _witnessService.GetByEmployeeWitnessAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpPost("cases/{caseId:guid}/witnesses")]
    public async Task<ActionResult<StaffDisciplineWitnessDto>> AddWitness(
        Guid caseId, [FromBody] CreateStaffDisciplineWitnessDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.DisciplinaryActionId = caseId;
        var created = await _witnessService.AddAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetWitnessById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("witnesses/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineWitnessDto>> UpdateWitness(
        Guid id, [FromBody] UpdateStaffDisciplineWitnessDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _witnessService.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("witnesses/{id:guid}")]
    public async Task<IActionResult> DeleteWitness(Guid id)
    {
        await _witnessService.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineDocumentSummaryDto>>> GetDocuments(Guid caseId)
        => Ok(await _documentService.GetByCaseIdAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/documents/scope/{scope}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineDocumentSummaryDto>>> GetDocumentsByScope(
        Guid caseId, DisciplinaryDocumentScope scope)
        => Ok(await _documentService.GetByScopeAsync(caseId, scope));

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/documents/category/{category}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineDocumentSummaryDto>>> GetDocumentsByCategory(
        Guid caseId, DisciplinaryDocumentCategory category)
        => Ok(await _documentService.GetByCategoryAsync(caseId, category));

    [Authorize(Roles = HrRoles)]
    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineDocumentDto?>> GetDocumentById(Guid id)
        => Ok(await _documentService.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("action-steps/{actionStepId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineDocumentSummaryDto>>> GetDocumentsByActionStep(Guid actionStepId)
        => Ok(await _documentService.GetByActionStepIdAsync(actionStepId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("appeals/{appealId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineDocumentSummaryDto>>> GetDocumentsByAppeal(Guid appealId)
        => Ok(await _documentService.GetByAppealIdAsync(appealId));

    [Authorize(Roles = HrRoles)]
    [HttpPost("cases/{caseId:guid}/documents")]
    public async Task<ActionResult<StaffDisciplineDocumentDto>> AddDocument(
        Guid caseId, [FromBody] CreateStaffDisciplineDocumentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        // A caller-supplied path would let anyone point a document row at arbitrary bytes on
        // disk, including another tenant's. Files arrive through the upload endpoint below,
        // which routes them past the malware scanner into private storage.
        if (!string.IsNullOrWhiteSpace(dto.FilePath) ||
            dto.FileUploadRecordId.HasValue ||
            dto.DocumentRecordId.HasValue ||
            dto.DocumentVersionId.HasValue)
        {
            return BadRequest(new
            {
                message = "File locations cannot be supplied directly. " +
                          "Use POST cases/{caseId}/documents/upload to attach a file."
            });
        }

        dto.DisciplinaryActionId = caseId;
        var created = await _documentService.AddAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetDocumentById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("cases/{caseId:guid}/documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<StaffDisciplineDocumentDto>> UploadDocument(
        Guid caseId,
        [FromForm] IFormFile file,
        [FromForm] DisciplinaryDocumentScope scope = DisciplinaryDocumentScope.Case,
        [FromForm] DisciplinaryDocumentCategory category = DisciplinaryDocumentCategory.Evidence,
        [FromForm] Guid? actionStepId = null,
        [FromForm] Guid? appealId = null,
        [FromForm] string? description = null,
        CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        if (!Guid.TryParse(_currentUser.UserId, out var actorUserId))
            return BadRequest("User context could not be resolved.");

        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        // Fail an incoherent scope before any bytes are stored.
        try
        {
            await _documentService.ValidateDocumentScopeAsync(
                caseId, scope, actionStepId, appealId, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId.Value,
                ActorUserId = actorUserId,
                ActorName = _currentUser.UserName,
                Category = ControlledFileUploadCategories.HrDisciplineDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Disciplinary case document",
                    SourceEntityType = "StaffDisciplinaryAction",
                    // The case id identifies the document in the DMS; it used to be baked into
                    // the storage folder, which fragmented per-category quota accounting.
                    SourceRecordId = caseId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = category.ToString(),
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
            var created = await _documentService.AddAsync(
                new CreateStaffDisciplineDocumentDto
                {
                    DisciplinaryActionId = caseId,
                    Scope = scope,
                    ActionStepId = scope == DisciplinaryDocumentScope.ActionStep ? actionStepId : null,
                    AppealId = scope == DisciplinaryDocumentScope.Appeal ? appealId : null,
                    FileName = document.OriginalFileName,
                    FilePath = string.Empty,
                    FileUploadRecordId = document.FileUploadRecordId,
                    DocumentRecordId = document.DocumentRecordId,
                    DocumentVersionId = document.DocumentVersionId,
                    Category = category,
                    Description = description,
                    UploadedById = employeeId.Value,
                    UploadDate = DateTime.UtcNow
                },
                tenantId.Value, employeeId.Value, ct);

            return CreatedAtAction(nameof(GetDocumentById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(document, tenantId.Value, actorUserId, ct);
            if (ex is ArgumentException)
                return BadRequest(new { message = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Streams a disciplinary document to a caller entitled to see it.
    /// </summary>
    /// <remarks>
    /// This endpoint previously checked only that the document belonged to the caller's tenant,
    /// so any authenticated user could read another employee's disciplinary evidence. Access is
    /// now limited to HR-equivalent roles and the employee the case concerns; confidential
    /// categories stay HR-only.
    /// </remarks>
    [Authorize(Roles = HrRoles)]
    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var document = await _db.Set<Core.Entities.HR.StaffDiscipline.StaffDisciplineDocument>()
            .AsNoTracking()
            .Include(item => item.DisciplinaryAction)
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (document is null)
            return NotFound();

        if (!CallerIsDisciplineReader())
        {
            // The subject may read their own case file — they need the notification and
            // decision letters to respond — but not the employer's legal advice about them.
            var isSubject = _currentUser.EmployeeId is Guid employeeId &&
                            document.DisciplinaryAction.EmployeeId == employeeId;
            if (!isSubject || document.Category == DisciplinaryDocumentCategory.LegalDocument)
                return Forbid();
        }

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        await _documentService.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/notes")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineNoteSummaryDto>>> GetNotes(
        Guid caseId, [FromQuery] bool includeConfidential = true)
        => Ok(await _noteService.GetByCaseIdAsync(caseId, includeConfidential));

    [Authorize(Roles = HrRoles)]
    [HttpGet("notes/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineNoteDto?>> GetNoteById(Guid id)
        => Ok(await _noteService.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("notes/by-author/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineNoteSummaryDto>>> GetNotesByAuthor(Guid employeeId)
        => Ok(await _noteService.GetByAuthorAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpPost("cases/{caseId:guid}/notes")]
    public async Task<ActionResult<StaffDisciplineNoteDto>> AddNote(
        Guid caseId, [FromBody] CreateStaffDisciplineNoteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.DisciplinaryActionId = caseId;
        var created = await _noteService.AddAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetNoteById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("notes/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineNoteDto>> UpdateNote(
        Guid id, [FromBody] UpdateStaffDisciplineNoteDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _noteService.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("notes/{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        await _noteService.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // NOTIFICATIONS
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/notifications")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineNotificationSummaryDto>>> GetNotifications(Guid caseId)
        => Ok(await _notificationService.GetByCaseIdAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/notifications/unacknowledged")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineNotificationSummaryDto>>> GetUnacknowledgedNotifications(Guid caseId)
        => Ok(await _notificationService.GetUnacknowledgedAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("notifications/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineNotificationDto?>> GetNotificationById(Guid id)
        => Ok(await _notificationService.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("notifications/pending-followup")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineNotificationSummaryDto>>> GetNotificationsPendingFollowup(
        [FromQuery] int daysOld = 3)
        => Ok(await _notificationService.GetPendingFollowupAsync(daysOld));

    [Authorize(Roles = HrRoles)]
    [HttpPost("cases/{caseId:guid}/notifications")]
    public async Task<ActionResult<StaffDisciplineNotificationDto>> SendNotification(
        Guid caseId, [FromBody] CreateStaffDisciplineNotificationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.DisciplinaryActionId = caseId;
        var created = await _notificationService.SendAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetNotificationById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Records that the subject received this notice. Deliberately ungated: the acknowledging party
    /// is by definition not HR, they are the employee the case was brought against. Identity is
    /// enforced in the service, which refuses the call from anyone but that employee.
    /// </summary>
    [HttpPost("notifications/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeNotification(Guid id, [FromBody] AcknowledgeNotificationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.NotificationId = id;
        await _notificationService.AcknowledgeAsync(dto, employeeId.Value);
        return Ok(new { message = "Notification acknowledged." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("notifications/{id:guid}/followup")]
    public async Task<IActionResult> SendFollowupNotification(Guid id, [FromBody] SendFollowupNotificationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.NotificationId = id;
        await _notificationService.SendFollowupAsync(dto);
        return Ok(new { message = "Follow-up notification recorded." });
    }

    // =========================================================================
    // LEGAL REVIEWS
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/legal-reviews")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineLegalReviewDto>>> GetLegalReviews(Guid caseId)
        => Ok(await _legalReviewService.GetByCaseIdAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("cases/{caseId:guid}/legal-reviews/total-costs")]
    public async Task<ActionResult<decimal>> GetTotalLegalCosts(Guid caseId)
        => Ok(await _legalReviewService.GetTotalLegalCostsForCaseAsync(caseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("legal-reviews/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineLegalReviewDto?>> GetLegalReviewById(Guid id)
        => Ok(await _legalReviewService.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("legal-reviews/open")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineLegalReviewDto>>> GetOpenLegalReviews()
        => Ok(await _legalReviewService.GetOpenReviewsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("legal-reviews/risk/{minimumRisk}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineLegalReviewDto>>> GetLegalReviewsByRisk(DisciplineLegalRiskLevel minimumRisk)
        => Ok(await _legalReviewService.GetByRiskLevelAsync(minimumRisk));

    [Authorize(Roles = HrRoles)]
    [HttpGet("legal-reviews/requiring-external-counsel")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineLegalReviewDto>>> GetRequiringExternalCounsel()
        => Ok(await _legalReviewService.GetRequiringExternalCounselAsync());

    [Authorize(Roles = HrRoles)]
    [HttpPost("cases/{caseId:guid}/legal-reviews")]
    public async Task<ActionResult<StaffDisciplineLegalReviewDto>> ReferToLegal(
        Guid caseId, [FromBody] CreateStaffDisciplineLegalReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.DisciplinaryActionId = caseId;
        var created = await _legalReviewService.ReferAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetLegalReviewById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("legal-reviews/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineLegalReviewDto>> UpdateLegalReview(
        Guid id, [FromBody] UpdateStaffDisciplineLegalReviewDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _legalReviewService.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("legal-reviews/{id:guid}/complete")]
    public async Task<IActionResult> CompleteLegalReview(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        await _legalReviewService.CompleteAsync(id, employeeId.Value);
        return Ok(new { message = "Legal review completed." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("legal-reviews/{id:guid}")]
    public async Task<IActionResult> DeleteLegalReview(Guid id)
    {
        await _legalReviewService.DeleteAsync(id);
        return NoContent();
    }
}
