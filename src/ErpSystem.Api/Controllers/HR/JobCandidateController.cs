using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Enums;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
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
/// Job candidates — the person behind an application, and the talent pool they may be kept in.
///
/// <para><b>HR-only, in full.</b> Unlike <see cref="JobVacancyController"/>, nothing here is an advert:
/// a candidate record holds a name, email, phone, date of birth, gender, CV, profile photo, referees
/// and recruiters' private notes on them. The controller previously carried a bare <c>[Authorize]</c>,
/// so any authenticated employee could read, edit or delete any of it — including
/// <c>GET {id}/notes?includePrivate=true</c>, which has no entitlement test of its own and is what
/// makes a note marked private actually private. There is no self-service surface on this controller,
/// so the gate sits at class level rather than being repeated on 40 endpoints.</para>
///
/// <para>Sub-resource writes take the candidate from the <b>route</b>. Each Create DTO also declares
/// <c>JobCandidateId</c>, and the service validated only that one — so a POST to
/// <c>/{A}/work-history</c> carrying <c>jobCandidateId: B</c> wrote to B's record.</para>
/// </summary>
[ApiController]
[Route("api/job-candidates")]
[Authorize(Roles = JobCandidateController.HrRoles)]
[RecruitmentBusinessRules]
public class JobCandidateController : ControllerBase
{
    internal const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IJobCandidateService _service;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<JobCandidateController> _logger;

    public JobCandidateController(
        IJobCandidateService service,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        IHrControlledDocumentService hrDocuments,
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<JobCandidateController> logger)
    {
        _service = service;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _hrDocuments = hrDocuments;
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    // =========================================================================
    // DOCUMENT DOWNLOADS (HR-side)
    // =========================================================================
    // Candidate files live in private storage and have no public URL. These are the
    // HR-facing counterparts of the candidate portal's own download endpoints.

    /// <summary>Streams a candidate's CV.</summary>
    [HttpGet("{id:guid}/cv")]
    public async Task<IActionResult> DownloadCv(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var candidate = await LoadCandidateAsync(id, tenantId, ct);
        if (candidate is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            candidate.CvDocumentRecordId, candidate.CvDocumentVersionId,
            candidate.CvFileUploadRecordId, candidate.CvFilePath,
            fallbackFileName: $"cv-{candidate.CandidateNumber}",
            fallbackContentType: null,
            inline: false, ct);
    }

    /// <summary>Streams a candidate's profile photo.</summary>
    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> DownloadPhoto(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var candidate = await LoadCandidateAsync(id, tenantId, ct);
        if (candidate is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            documentRecordId: null, documentVersionId: null,
            candidate.ProfilePhotoFileUploadRecordId,
            legacyPath: null,
            fallbackFileName: "profile-photo",
            fallbackContentType: null,
            inline: true, ct);
    }

    /// <summary>Streams one of a candidate's uploaded documents.</summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(
        Guid id, Guid documentId, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        // Scoped to the candidate named in the route, so a document id from another
        // candidate is a lookup miss rather than a disclosure.
        var document = await _db.Set<JobCandidateDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == documentId &&
                        item.JobCandidateId == id &&
                        item.TenantId == tenantId &&
                        !item.IsDeleted,
                ct);
        if (document is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    private Task<JobCandidate?> LoadCandidateAsync(
        Guid id, Guid tenantId, CancellationToken ct)
        => _db.Set<JobCandidate>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<JobCandidateSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<JobCandidateSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobCandidateDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{candidateNumber}")]
    public async Task<ActionResult<JobCandidateDto?>> GetByCandidateNumber(string candidateNumber)
        => Ok(await _service.GetByCandidateNumberAsync(candidateNumber));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<JobCandidateDetailDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("talent-pool")]
    public async Task<ActionResult<IEnumerable<JobCandidateSummaryDto>>> GetTalentPool()
        => Ok(await _service.GetTalentPoolAsync());

    [HttpGet("vacancy/{vacancyId:guid}")]
    public async Task<ActionResult<IEnumerable<JobCandidateSummaryDto>>> GetByVacancy(Guid vacancyId)
        => Ok(await _service.GetByVacancyIdAsync(vacancyId));

    [HttpGet("email/{email}")]
    public async Task<ActionResult<JobCandidateDto?>> GetByEmail(string email)
        => Ok(await _service.GetByEmailAsync(email));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<JobCandidateDto>> Create([FromBody] CreateJobCandidateDto dto)
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
    public async Task<ActionResult<JobCandidateDto>> Update(Guid id, [FromBody] UpdateJobCandidateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // TALENT POOL
    // =========================================================================

    [HttpPost("{id:guid}/add-to-talent-pool")]
    public async Task<IActionResult> AddToTalentPool(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.AddToTalentPoolAsync(id, employeeId.Value);
        return Ok(new { message = "Candidate added to talent pool." });
    }

    [HttpPost("{id:guid}/remove-from-talent-pool")]
    public async Task<IActionResult> RemoveFromTalentPool(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RemoveFromTalentPoolAsync(id, employeeId.Value);
        return Ok(new { message = "Candidate removed from talent pool." });
    }

    // =========================================================================
    // QUALIFICATIONS
    // =========================================================================

    [HttpGet("{candidateId:guid}/qualifications")]
    public async Task<ActionResult<IEnumerable<JobCandidateQualificationDto>>> GetQualifications(Guid candidateId)
        => Ok(await _service.GetQualificationsAsync(candidateId));

    [HttpPost("{candidateId:guid}/qualifications")]
    public async Task<ActionResult<JobCandidateQualificationDto>> AddQualification(
        Guid candidateId, [FromBody] CreateJobCandidateQualificationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobCandidateId = candidateId;
        return Ok(await _service.AddQualificationAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{candidateId:guid}/qualifications/{qualificationId:guid}")]
    public async Task<ActionResult<JobCandidateQualificationDto>> UpdateQualification(
        Guid candidateId, Guid qualificationId, [FromBody] UpdateJobCandidateQualificationDto dto)
    {
        if (qualificationId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateQualificationAsync(dto, employeeId.Value));
    }

    [HttpDelete("qualifications/{qualificationId:guid}")]
    public async Task<IActionResult> DeleteQualification(Guid qualificationId)
    {
        await _service.DeleteQualificationAsync(qualificationId);
        return NoContent();
    }

    // =========================================================================
    // WORK HISTORY
    // =========================================================================

    [HttpGet("{candidateId:guid}/work-history")]
    public async Task<ActionResult<IEnumerable<JobCandidateWorkHistoryDto>>> GetWorkHistory(Guid candidateId)
        => Ok(await _service.GetWorkHistoriesAsync(candidateId));

    [HttpPost("{candidateId:guid}/work-history")]
    public async Task<ActionResult<JobCandidateWorkHistoryDto>> AddWorkHistory(
        Guid candidateId, [FromBody] CreateJobCandidateWorkHistoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobCandidateId = candidateId;
        return Ok(await _service.AddWorkHistoryAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{candidateId:guid}/work-history/{workHistoryId:guid}")]
    public async Task<ActionResult<JobCandidateWorkHistoryDto>> UpdateWorkHistory(
        Guid candidateId, Guid workHistoryId, [FromBody] UpdateJobCandidateWorkHistoryDto dto)
    {
        if (workHistoryId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateWorkHistoryAsync(dto, employeeId.Value));
    }

    [HttpDelete("work-history/{workHistoryId:guid}")]
    public async Task<IActionResult> DeleteWorkHistory(Guid workHistoryId)
    {
        await _service.DeleteWorkHistoryAsync(workHistoryId);
        return NoContent();
    }

    // =========================================================================
    // REFEREES
    // =========================================================================

    [HttpGet("{candidateId:guid}/referees")]
    public async Task<ActionResult<IEnumerable<JobCandidateRefereeDto>>> GetReferees(Guid candidateId)
        => Ok(await _service.GetRefereesAsync(candidateId));

    [HttpPost("{candidateId:guid}/referees")]
    public async Task<ActionResult<JobCandidateRefereeDto>> AddReferee(
        Guid candidateId, [FromBody] CreateJobCandidateRefereeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobCandidateId = candidateId;
        return Ok(await _service.AddRefereeAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{candidateId:guid}/referees/{refereeId:guid}")]
    public async Task<ActionResult<JobCandidateRefereeDto>> UpdateReferee(
        Guid candidateId, Guid refereeId, [FromBody] UpdateJobCandidateRefereeDto dto)
    {
        if (refereeId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateRefereeAsync(dto, employeeId.Value));
    }

    [HttpDelete("referees/{refereeId:guid}")]
    public async Task<IActionResult> DeleteReferee(Guid refereeId)
    {
        await _service.DeleteRefereeAsync(refereeId);
        return NoContent();
    }

    // =========================================================================
    // SKILLS
    // =========================================================================

    [HttpGet("{candidateId:guid}/skills")]
    public async Task<ActionResult<IEnumerable<JobCandidateSkillDto>>> GetSkills(Guid candidateId)
        => Ok(await _service.GetSkillsAsync(candidateId));

    [HttpPost("{candidateId:guid}/skills")]
    public async Task<ActionResult<JobCandidateSkillDto>> AddSkill(
        Guid candidateId, [FromBody] CreateJobCandidateSkillDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobCandidateId = candidateId;
        return Ok(await _service.AddSkillAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{candidateId:guid}/skills/{skillId:guid}")]
    public async Task<ActionResult<JobCandidateSkillDto>> UpdateSkill(
        Guid candidateId, Guid skillId, [FromBody] UpdateJobCandidateSkillDto dto)
    {
        if (skillId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateSkillAsync(dto, employeeId.Value));
    }

    [HttpDelete("skills/{skillId:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid skillId)
    {
        await _service.DeleteSkillAsync(skillId);
        return NoContent();
    }

    // =========================================================================
    // INTERESTS
    // =========================================================================

    [HttpGet("{candidateId:guid}/interests")]
    public async Task<ActionResult<IEnumerable<JobCandidateInterestDto>>> GetInterests(Guid candidateId)
        => Ok(await _service.GetInterestsAsync(candidateId));

    [HttpPost("{candidateId:guid}/interests")]
    public async Task<ActionResult<JobCandidateInterestDto>> AddInterest(
        Guid candidateId, [FromBody] CreateJobCandidateInterestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobCandidateId = candidateId;
        return Ok(await _service.AddInterestAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{candidateId:guid}/interests/{interestId:guid}")]
    public async Task<ActionResult<JobCandidateInterestDto>> UpdateInterest(
        Guid candidateId, Guid interestId, [FromBody] UpdateJobCandidateInterestDto dto)
    {
        if (interestId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateInterestAsync(dto, employeeId.Value));
    }

    [HttpDelete("interests/{interestId:guid}")]
    public async Task<IActionResult> DeleteInterest(Guid interestId)
    {
        await _service.DeleteInterestAsync(interestId);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{candidateId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<JobCandidateDocumentDto>>> GetDocuments(Guid candidateId)
        => Ok(await _service.GetDocumentsAsync(candidateId));

    /// <summary>
    /// Attaches a document to a candidate through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// Replaces a JSON endpoint that accepted a caller-supplied <c>filePath</c>: it stored no file,
    /// scanned nothing, and recorded a path to a file the server had never received — so the row it
    /// wrote could never be downloaded. Seventh sibling of the same shape, after the five appraisal
    /// paths and the requisition attachments.
    ///
    /// <para>The DMS columns already existed on <c>JobCandidateDocument</c> — the candidate portal has
    /// written through the gate since the documents commit — so this needs no migration; only the
    /// HR-side write path was still on the old shape.</para>
    ///
    /// <para>⚠ Requires a working ClamAV — <c>hr-recruitment-attachments</c> is in
    /// <c>SystemCleanScanRequired</c> and tenant policy cannot turn that off, so with no scanner the
    /// gate refuses with 422 before the row is ever written.</para>
    /// </remarks>
    [HttpPost("{candidateId:guid}/documents")]
    [ProducesResponseType(typeof(JobCandidateDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddDocument(
        Guid candidateId,
        IFormFile file,
        [FromForm] JobCandidateDocumentType documentType,
        [FromForm] string? description,
        CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "JobCandidate",
            sourceRecordId: candidateId,
            sourceLabel: "Candidate document",
            documentType: "JobCandidateDocument",
            description: description,
            persist: (uploadedById, document) => _service.AddDocumentAsync(
                candidateId, documentType, document.OriginalFileName,
                tenantId, uploadedById, ct,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrRecruitmentAttachments);
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [HttpGet("{candidateId:guid}/notes")]
    public async Task<ActionResult<IEnumerable<JobCandidateNoteDto>>> GetNotes(
        Guid candidateId, [FromQuery] bool includePrivate = false)
        => Ok(await _service.GetNotesAsync(candidateId, includePrivate));

    [HttpPost("{candidateId:guid}/notes")]
    public async Task<ActionResult<JobCandidateNoteDto>> AddNote(
        Guid candidateId, [FromBody] CreateJobCandidateNoteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobCandidateId = candidateId;
        return Ok(await _service.AddNoteAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{candidateId:guid}/notes/{noteId:guid}")]
    public async Task<ActionResult<JobCandidateNoteDto>> UpdateNote(
        Guid candidateId, Guid noteId, [FromBody] UpdateJobCandidateNoteDto dto)
    {
        if (noteId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateNoteAsync(dto, employeeId.Value));
    }

    [HttpDelete("notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid noteId)
    {
        await _service.DeleteNoteAsync(noteId);
        return NoContent();
    }
}
