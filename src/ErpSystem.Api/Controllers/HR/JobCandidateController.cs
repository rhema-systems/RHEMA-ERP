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
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class JobCandidateController : ControllerBase
{
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
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
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

    /// <summary>
    /// Sets a candidate's profile photograph from HR (round 3, lane C2).
    /// </summary>
    /// <remarks>
    /// The careers side has uploaded photographs through the gate since the documents commit, but a
    /// candidate HR records by hand — a walk-in, a referral — had no door at all, and the HR screens
    /// never rendered the photograph either way. Same category as the careers upload
    /// (<c>hr-candidate-photos</c>), no DMS registration (an avatar carries no retention value), and
    /// the same two writes on the record. Answers the same shape as the careers door: the gated
    /// route to fetch the image, never a public URL.
    /// </remarks>
    [HttpPost("{id:guid}/photo")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return BadRequest("The signed-in user could not be resolved.");

        // Owned by the tenant before any bytes are stored — a miss is a 404, not an orphaned upload.
        var candidate = await LoadCandidateAsync(id, tenantId, ct);
        if (candidate is null)
            return NotFound();

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = userId,
                ActorName = _currentUser.UserName ?? "hr",
                Category = ControlledFileUploadCategories.HrCandidatePhotos,
                File = file,
                Registration = null,
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            await _service.SetProfilePhotoAsync(id, document.FileUploadRecordId, userId, ct);
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(document, tenantId, userId, ct);
            if (ex is ArgumentException)
                return NotFound(new { message = ex.Message });
            throw;
        }

        return Ok(new { url = Url.Action(nameof(DownloadPhoto), new { id }), hasPhoto = true });
    }

    /// <summary>Streams one of a candidate's uploaded documents.</summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<PagedResult<JobCandidateSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        // G-7.6: name, email, phone, headline, current title or employer. The register's only
        // lookup used to be an exact-email match.
        [FromQuery] string? search = null)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, search));

    [HttpGet("all")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobCandidateDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{candidateNumber}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobCandidateDto?>> GetByCandidateNumber(string candidateNumber)
        => Ok(await _service.GetByCandidateNumberAsync(candidateNumber));

    [HttpGet("{id:guid}/details")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobCandidateDetailDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("talent-pool")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateSummaryDto>>> GetTalentPool()
        => Ok(await _service.GetTalentPoolAsync());

    [HttpGet("vacancy/{vacancyId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateSummaryDto>>> GetByVacancy(Guid vacancyId)
        => Ok(await _service.GetByVacancyIdAsync(vacancyId));

    [HttpGet("email/{email}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobCandidateDto?>> GetByEmail(string email)
        => Ok(await _service.GetByEmailAsync(email));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // TALENT POOL
    // =========================================================================

    // ⚠ POST {id}/add-to-talent-pool and POST {id}/remove-from-talent-pool were RETIRED here on
    // 2026-09-15 (G-7.4). Both were superseded by the richer TalentPoolController endpoints the UI
    // now uses, and neither had a caller left — but leaving a live door onto them mattered, because
    // the old pair set IsInTalentPool with **no source, no reason and no review date**, which is
    // exactly the data loss the replacement was written to stop. An endpoint that silently
    // downgrades a record is worse than one that does not exist.
    //
    // Entry to and exit from the pool now go through TalentPoolController, which records who put
    // the candidate there, why, and when they should next be looked at.

    // =========================================================================
    // QUALIFICATIONS
    // =========================================================================

    [HttpGet("{candidateId:guid}/qualifications")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateQualificationDto>>> GetQualifications(Guid candidateId)
        => Ok(await _service.GetQualificationsAsync(candidateId));

    [HttpPost("{candidateId:guid}/qualifications")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteQualification(Guid qualificationId)
    {
        await _service.DeleteQualificationAsync(qualificationId);
        return NoContent();
    }

    // =========================================================================
    // WORK HISTORY
    // =========================================================================

    [HttpGet("{candidateId:guid}/work-history")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateWorkHistoryDto>>> GetWorkHistory(Guid candidateId)
        => Ok(await _service.GetWorkHistoriesAsync(candidateId));

    [HttpPost("{candidateId:guid}/work-history")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteWorkHistory(Guid workHistoryId)
    {
        await _service.DeleteWorkHistoryAsync(workHistoryId);
        return NoContent();
    }

    // =========================================================================
    // REFEREES
    // =========================================================================

    [HttpGet("{candidateId:guid}/referees")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateRefereeDto>>> GetReferees(Guid candidateId)
        => Ok(await _service.GetRefereesAsync(candidateId));

    [HttpPost("{candidateId:guid}/referees")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteReferee(Guid refereeId)
    {
        await _service.DeleteRefereeAsync(refereeId);
        return NoContent();
    }

    // =========================================================================
    // SKILLS
    // =========================================================================

    [HttpGet("{candidateId:guid}/skills")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateSkillDto>>> GetSkills(Guid candidateId)
        => Ok(await _service.GetSkillsAsync(candidateId));

    [HttpPost("{candidateId:guid}/skills")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteSkill(Guid skillId)
    {
        await _service.DeleteSkillAsync(skillId);
        return NoContent();
    }

    // =========================================================================
    // LANGUAGES (round 3, lane C1)
    // =========================================================================

    [HttpGet("{candidateId:guid}/languages")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateLanguageDto>>> GetLanguages(Guid candidateId)
        => Ok(await _service.GetLanguagesAsync(candidateId));

    [HttpPost("{candidateId:guid}/languages")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobCandidateLanguageDto>> AddLanguage(
        Guid candidateId, [FromBody] CreateJobCandidateLanguageDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.JobCandidateId = candidateId;
        return Ok(await _service.AddLanguageAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("{candidateId:guid}/languages/{languageId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobCandidateLanguageDto>> UpdateLanguage(
        Guid candidateId, Guid languageId, [FromBody] UpdateJobCandidateLanguageDto dto)
    {
        if (languageId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateLanguageAsync(dto, employeeId.Value));
    }

    [HttpDelete("languages/{languageId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteLanguage(Guid languageId)
    {
        await _service.DeleteLanguageAsync(languageId);
        return NoContent();
    }

    // =========================================================================
    // INTERESTS
    // =========================================================================

    [HttpGet("{candidateId:guid}/interests")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateInterestDto>>> GetInterests(Guid candidateId)
        => Ok(await _service.GetInterestsAsync(candidateId));

    [HttpPost("{candidateId:guid}/interests")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteInterest(Guid interestId)
    {
        await _service.DeleteInterestAsync(interestId);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{candidateId:guid}/documents")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId,
                description),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrRecruitmentAttachments);
    }

    [HttpDelete("documents/{documentId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [HttpGet("{candidateId:guid}/notes")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobCandidateNoteDto>>> GetNotes(
        Guid candidateId, [FromQuery] bool includePrivate = false)
        => Ok(await _service.GetNotesAsync(candidateId, includePrivate));

    [HttpPost("{candidateId:guid}/notes")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
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
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteNote(Guid noteId)
    {
        await _service.DeleteNoteAsync(noteId);
        return NoContent();
    }
}
