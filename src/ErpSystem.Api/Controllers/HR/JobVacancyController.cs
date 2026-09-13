using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Recruitment;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Job vacancies — the advertised role a requisition turns into.
///
/// <para>Vacancy status deliberately stays off the generic workflow engine, unlike
/// <c>StaffRequisition</c>. It has several writers — the edit-resets-to-Draft rule, publication's
/// auto-created postings, closing for applications, and the hiring stages that follow — which is
/// the <c>AppraisalStatus</c> shape, not the single-writer approval lifecycle the engine is for.
/// The approval gate it does have is enforced by the service's transition map instead.</para>
///
/// <para>Reads are open to the tenant: a vacancy is an internal advert, and the hiring managers,
/// recruiters and candidates-to-be all need to see it. Everything that changes one is HR's.</para>
/// </summary>
[ApiController]
[Route("api/job-vacancies")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class JobVacancyController : ControllerBase
{
    private readonly IJobVacancyService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<JobVacancyController> _logger;

    public JobVacancyController(
        IJobVacancyService service,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ILogger<JobVacancyController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorageService = fileStorageService;
        _db = db;
        _logger = logger;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<PagedResult<JobVacancySummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobVacancyDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{vacancyNumber}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobVacancyDto?>> GetByVacancyNumber(string vacancyNumber)
        => Ok(await _service.GetByVacancyNumberAsync(vacancyNumber));

    [HttpGet("{id:guid}/details")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobVacancyDetailDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByStatus(JobVacancyStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetActive()
        => Ok(await _service.GetActiveVacanciesAsync());

    /// <summary>
    /// The internal job board — published vacancies open to internal candidates.
    /// </summary>
    /// <remarks>
    /// Deliberately not recruitment-gated: every employee may see what is open. That is exactly
    /// why it serves the LEAN <see cref="PublicVacancyDto"/> rather than the full vacancy record,
    /// and why <c>AllowInternalCandidates</c> is applied in the service rather than in the screen.
    /// See <c>JobVacancyService.GetPublishedForJobBoardAsync</c>.
    /// </remarks>
    [HttpGet("published")]
    public async Task<ActionResult<IEnumerable<PublicVacancyDto>>> GetPublished(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        return Ok(await _service.GetPublishedForJobBoardAsync(tenantId.Value, ct));
    }

    [HttpGet("position/{positionId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByPosition(Guid positionId)
        => Ok(await _service.GetByPositionAsync(positionId));

    [HttpGet("hiring-manager/{hiringManagerId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByHiringManager(Guid hiringManagerId)
        => Ok(await _service.GetByHiringManagerAsync(hiringManagerId));

    [HttpGet("recruiter/{recruiterId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByRecruiter(Guid recruiterId)
        => Ok(await _service.GetByRecruiterAsync(recruiterId));

    [HttpGet("requisition/{requisitionId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetByRequisition(Guid requisitionId)
        => Ok(await _service.GetByRequisitionAsync(requisitionId));

    [HttpGet("deadline-approaching")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancySummaryDto>>> GetDeadlineApproaching(
        [FromQuery] int daysAhead = 7)
        => Ok(await _service.GetWithDeadlineApproachingAsync(daysAhead));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobVacancyDto>> Create([FromBody] CreateJobVacancyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobVacancyDto>> Update(Guid id, [FromBody] UpdateJobVacancyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    /// <summary>
    /// Atomically saves field updates AND applies a status transition in a single DB transaction.
    /// Use this from any transition button on the edit form instead of a separate UpdateAsync
    /// + ChangeStatusAsync pair to avoid partial-failure risk.
    /// </summary>
    [HttpPost("{id:guid}/transition")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobVacancyDto>> Transition(Guid id, [FromBody] TransitionJobVacancyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null || employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var result = await _service.TransitionAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(result);
    }

    [HttpPost("{id:guid}/change-status")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeJobVacancyStatusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ChangeStatusAsync(dto, employeeId.Value);
        return Ok(new { message = "Vacancy status updated." });
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseJobVacancyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CloseAsync(dto, employeeId.Value);
        return Ok(new { message = "Vacancy cancelled." });
    }

    [HttpPost("{id:guid}/close-for-applications")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> CloseForApplications(Guid id, [FromBody] CloseForApplicationsDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CloseForApplicationsAsync(dto, employeeId.Value);
        return Ok(new { message = "Vacancy closed for applications." });
    }

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    [HttpGet("{vacancyId:guid}/attachments")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancyAttachmentDto>>> GetAttachments(Guid vacancyId)
        => Ok(await _service.GetAttachmentsAsync(vacancyId));

    /// <summary>
    /// Attaches a document to a vacancy through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// Replaced a JSON endpoint that accepted a caller-supplied <c>filePath</c>: it stored no file,
    /// scanned nothing, and recorded whatever path was posted.
    ///
    /// <para>⚠ Requires a working ClamAV — <c>hr-recruitment-attachments</c> is scan-mandatory and
    /// tenant policy cannot turn that off.</para>
    /// </remarks>
    [HttpPost("{vacancyId:guid}/attachments")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    [ProducesResponseType(typeof(JobVacancyAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        Guid vacancyId, IFormFile file, [FromForm] string? description, CancellationToken ct)
    {
        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "JobVacancy",
            sourceRecordId: vacancyId,
            sourceLabel: "Job vacancy attachment",
            documentType: "JobVacancyAttachment",
            description: description,
            persist: (uploadedById, document) => _service.AddAttachmentAsync(
                vacancyId, uploadedById, document.OriginalFileName, document.FileSize, description, ct,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrRecruitmentAttachments);
    }

    /// <summary>Streams a vacancy attachment — the file lives outside the web root.</summary>
    [HttpGet("{vacancyId:guid}/attachments/{attachmentId:guid}/download")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid vacancyId, Guid attachmentId, CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<JobVacancyAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.JobVacancyId == vacancyId && a.TenantId == tenantId && !a.IsDeleted,
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

    [HttpDelete("attachments/{attachmentId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        await _service.DeleteAttachmentAsync(attachmentId);
        return NoContent();
    }

    // =========================================================================
    // STATUS HISTORY
    // =========================================================================

    [HttpGet("{vacancyId:guid}/status-history")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobVacancyStatusHistoryDto>>> GetStatusHistory(Guid vacancyId)
        => Ok(await _service.GetStatusHistoryAsync(vacancyId));

    [HttpGet("{vacancyId:guid}/status-history/latest")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobVacancyStatusHistoryDto?>> GetLatestStatusHistory(Guid vacancyId)
        => Ok(await _service.GetLatestStatusHistoryAsync(vacancyId));

    // =========================================================================
    // SHORTLISTING CRITERIA
    // =========================================================================

    /// <summary>
    /// What each criterion type is made of — its value source, whether it may be mandatory, whether
    /// it names a protected characteristic (round 3, lane K; plan § 5.4). One table on the server,
    /// so the criteria panel no longer carries a hand-copied map that drifts from the engine.
    /// </summary>
    [HttpGet("criteria/shapes")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public ActionResult<IEnumerable<ShortlistingCriteriaShapeDto>> GetCriteriaShapes()
        => Ok(ShortlistingCriteriaShapes.All.Select(s => s.ToDto()));

    [HttpGet("{vacancyId:guid}/criteria")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobShortlistingCriteriaDto>>> GetCriteria(Guid vacancyId)
        => Ok(await _service.GetCriteriaAsync(vacancyId));

    [HttpGet("{vacancyId:guid}/criteria/mandatory")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobShortlistingCriteriaDto>>> GetMandatoryCriteria(Guid vacancyId)
        => Ok(await _service.GetMandatoryCriteriaAsync(vacancyId));

    [HttpPost("{vacancyId:guid}/criteria")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobShortlistingCriteriaDto>> AddCriteria(
        Guid vacancyId, [FromBody] CreateJobShortlistingCriteriaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        try
        {
            return Ok(await _service.AddCriteriaAsync(dto, tenantId.Value, employeeId.Value));
        }
        catch (InvalidOperationException ex)
        {
            return CriterionRuleRejected(ex, "adding a shortlisting criterion");
        }
    }

    [HttpPut("criteria/{criteriaId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobShortlistingCriteriaDto>> UpdateCriteria(
        Guid criteriaId, [FromBody] UpdateJobShortlistingCriteriaDto dto)
    {
        if (criteriaId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        try
        {
            return Ok(await _service.UpdateCriteriaAsync(dto, employeeId.Value));
        }
        catch (InvalidOperationException ex)
        {
            return CriterionRuleRejected(ex, "updating a shortlisting criterion");
        }
    }

    /// <summary>
    /// Reports a criterion rule in its own words.
    /// </summary>
    /// <remarks>
    /// ⚠ Without this the refusal reaches nobody: <c>GlobalExceptionHandlingMiddleware</c> maps
    /// <see cref="InvalidOperationException"/> to a fixed string and DISCARDS the message, so a
    /// carefully-worded rule arrives at the screen as a generic error. Same shape as the three
    /// mute refusals lane 3c had to fix.
    ///
    /// <para>⚠ Returns <see cref="ActionResult"/>, not <c>IActionResult</c>: both callers are
    /// declared <c>ActionResult&lt;JobShortlistingCriteriaDto&gt;</c>, and that type has an
    /// implicit conversion from <c>ActionResult</c> only.</para>
    /// </remarks>
    private ActionResult CriterionRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Shortlisting criterion rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    [HttpDelete("criteria/{criteriaId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteCriteria(Guid criteriaId)
    {
        await _service.DeleteCriteriaAsync(criteriaId);
        return NoContent();
    }

    // ── Pipeline stage assignments ────────────────────────────────────────────

    [HttpGet("{vacancyId:guid}/stage-assignments")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<VacancyPipelineStageAssignmentDto>>> GetStageAssignments(Guid vacancyId)
        => Ok(await _service.GetStageAssignmentsAsync(vacancyId));

    [HttpPost("{vacancyId:guid}/stage-assignments")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> UpsertStageAssignment(
        Guid vacancyId, [FromBody] CreateVacancyPipelineStageAssignmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.JobVacancyId != vacancyId) return BadRequest("VacancyId mismatch.");

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var result = await _service.UpsertStageAssignmentAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(result);
    }

    [HttpPut("stage-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> UpdateStageAssignment(
        Guid id, [FromBody] UpdateVacancyPipelineStageAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateStageAssignmentAsync(dto, employeeId.Value));
    }

    [HttpPatch("stage-assignments/{id:guid}/complete")]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> CompleteStageAssignment(
        Guid id, [FromBody] CompleteVacancyPipelineStageAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.CompleteStageAssignmentAsync(dto, employeeId.Value));
    }

    [HttpPatch("stage-assignments/{id:guid}/skip")]
    public async Task<ActionResult<VacancyPipelineStageAssignmentDto>> SkipStageAssignment(
        Guid id, [FromBody] SkipVacancyPipelineStageAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.SkipStageAssignmentAsync(dto, employeeId.Value));
    }

    [HttpDelete("stage-assignments/{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteStageAssignment(Guid id)
    {
        var deleted = await _service.DeleteStageAssignmentAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
