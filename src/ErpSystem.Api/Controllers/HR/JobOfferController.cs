using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
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
/// Employment offers — terms, approval, issue, and the candidate's response.
///
/// <para><b>HR-only, reads included.</b> The controller previously carried a bare
/// <c>[Authorize]</c>, so any authenticated employee could read every offer in the tenant — which
/// is every new hire's salary, bonus and benefits — and create, approve, issue or revoke them.</para>
///
/// <para><b>Approval runs on the generic workflow engine</b>, like the two appraisal outcome
/// proposals: an offer is a single-writer approval lifecycle that commits money, and who signs off
/// an offer above the band midpoint is a routing policy rather than something to hard-code.
/// ⚠ Submit/approve/reject are therefore inoperable until a <c>JobOffer</c> definition is published
/// and <c>POST api/Workflow/entity-types/seed</c> has been re-run — authority comes from the
/// definition, not from the role gate here. Everything from <c>Sent</c> onwards stays a direct
/// action: issuing, the candidate responding, negotiating, revising and revoking have several
/// writers, including the candidate through the anonymous token flow.</para>
/// </summary>
[ApiController]
[Route("api/job-offers")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class JobOfferController : ControllerBase
{
    private readonly IJobOfferService _service;
    private readonly IOfferLetterService _offerLetter;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public JobOfferController(
        IJobOfferService service,
        IOfferLetterService offerLetter,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _service = service;
        _offerLetter = offerLetter;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetAll()
        => Ok(await _service.GetAllSummaryAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobOfferDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{offerNumber}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobOfferDto?>> GetByOfferNumber(string offerNumber)
        => Ok(await _service.GetByOfferNumberAsync(offerNumber));

    [HttpGet("{id:guid}/details")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobOfferDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("application/{applicationId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<JobOfferDto?>> GetByApplication(Guid applicationId)
        => Ok(await _service.GetByApplicationIdAsync(applicationId));

    /// <summary>
    /// Generates the formal offer-of-employment letter for internal preview / print-to-PDF. The
    /// letter is rendered from the HR-editable "OfferLetter" template enriched with the offer terms,
    /// job-description summary + duties, itemised salary breakdown, benefits and pre-employment
    /// conditions.
    ///
    /// <para>⚠ Route renamed from <c>{id}/letter</c> to <c>{id}/letter-preview</c>. It collided
    /// exactly with <see cref="DownloadLetter"/> below, which streams the stored PDF on the same
    /// verb and template — so ASP.NET raised <c>AmbiguousMatchException</c> on every request and
    /// <b>both</b> endpoints were dead. The rendered preview and the stored file are genuinely
    /// different resources, so they get different routes rather than one being dropped.</para>
    /// </summary>
    [HttpGet("{id:guid}/letter-preview")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<OfferLetterDto>> GetOfferLetter(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _offerLetter.GenerateAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetByStatus(JobOfferStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("expiring")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetExpiring(
        [FromQuery] int daysAhead = 7)
        => Ok(await _service.GetExpiringOffersAsync(daysAhead));

    [HttpGet("prepared-by/{employeeId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetByPreparedBy(Guid employeeId)
        => Ok(await _service.GetByPreparedByAsync(employeeId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobOfferDto>> Create([FromBody] CreateJobOfferDto dto)
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
    public async Task<ActionResult<JobOfferDto>> Update(Guid id, [FromBody] UpdateJobOfferDto dto)
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

    [HttpPost("{id:guid}/submit-for-approval")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> SubmitForApproval(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.SubmitForApprovalAsync(id);
        return Ok(new { message = "Offer submitted for approval." });
    }

    /// <summary>Withdraws an offer that is out for approval, returning it to Draft.</summary>
    [HttpPost("{id:guid}/recall")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> Recall(Guid id)
    {
        await _service.RecallApprovalAsync(id);
        return Ok(new { message = "Offer recalled." });
    }

    // W3 slice 9: approve/reject-approval deliberately carry no permission attribute — the service
    // validates the caller against the pending workflow step (CanUserApproveAsync, no legacy
    // fallback), and a permission here would refuse non-HR approvers the definition names. The
    // class gate was previously Roles=SuperAdmin,HR, which could refuse the true assignee.
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveJobOfferDto dto)
    {
        // The service keys off the body's id, so a mismatch used to act on a different offer.
        dto.OfferId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ApproveAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer approved." });
    }

    [HttpPost("{id:guid}/reject-approval")]
    public async Task<IActionResult> RejectApproval(Guid id, [FromBody] RejectJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.OfferId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RejectApprovalAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer approval rejected." });
    }

    [HttpPost("{id:guid}/issue")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssueJobOfferDto dto)
    {
        dto.OfferId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.IssueAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer issued." });
    }

    [HttpPost("{id:guid}/record-response")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> RecordResponse(Guid id, [FromBody] RecordOfferResponseDto dto)
    {
        dto.OfferId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordResponseAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer response recorded." });
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeJobOfferDto dto)
    {
        dto.OfferId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RevokeAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer revoked." });
    }

    [HttpPost("{id:guid}/accept-conditionally")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobOfferDto>> AcceptConditionally(Guid id, [FromBody] string? candidateResponseNotes = null)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var result = await _service.AcceptConditionallyAsync(id, employeeId.Value, candidateResponseNotes);
        return Ok(result);
    }

    [HttpPost("{id:guid}/revise")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobOfferDto>> Revise(Guid id, [FromBody] ReviseJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.OriginalOfferId = id;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var revised = await _service.ReviseOfferAsync(dto, employeeId.Value);
        return Ok(revised);
    }

    // =========================================================================
    // BENEFITS — POSITION GRADE INTEGRATION
    // =========================================================================

    /// <summary>
    /// The offer's benefit lines.
    ///
    /// <para>⚠ These four routes are new. <c>IJobOfferService</c> has carried
    /// <c>AddBenefitAsync</c>, <c>GetBenefitsAsync</c>, <c>UpdateBenefitAsync</c> and
    /// <c>DeleteBenefitAsync</c> — implemented, tenant-scoped and status-guarded — since the port,
    /// and <b>nothing routed to any of them</b>. Only the position-grade import below was reachable,
    /// so a negotiated line the grade cannot supply (relocation, a car allowance) could be seeded
    /// from a position and then never corrected or removed. Another whole feature that had never
    /// executed; see hr-dead-path-defects.</para>
    ///
    /// <para>Editing is confined to Draft and PendingApproval by the service, like adding and
    /// removing — the terms stop being negotiable once an offer is approved, and a revision is the
    /// route to changing them after that.</para>
    /// </summary>
    [HttpGet("{id:guid}/benefits")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferBenefitDto>>> GetBenefits(Guid id)
        => Ok(await _service.GetBenefitsAsync(id));

    [HttpPost("{id:guid}/benefits")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobOfferBenefitDto>> AddBenefit(
        Guid id, [FromBody] CreateJobOfferBenefitDto dto)
    {
        // The route owns the offer id — the body's was free to name a different one.
        dto.JobOfferId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddBenefitAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("benefits/{benefitId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobOfferBenefitDto>> UpdateBenefit(
        Guid benefitId, [FromBody] UpdateJobOfferBenefitDto dto)
    {
        if (benefitId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateBenefitAsync(dto, employeeId.Value));
    }

    [HttpDelete("benefits/{benefitId:guid}")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    public async Task<IActionResult> DeleteBenefit(Guid benefitId)
    {
        await _service.DeleteBenefitAsync(benefitId);
        return NoContent();
    }

    /// <summary>Preview benefits from the position grade without persisting them.</summary>
    [HttpGet("{id:guid}/suggest-benefits")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferBenefitDto>>> SuggestBenefits(
        Guid id, CancellationToken ct)
        => Ok(await _service.SuggestBenefitsFromPositionAsync(id, ct));

    /// <summary>Import position-grade benefits into the offer (deduped, persists to DB).</summary>
    [HttpPost("{id:guid}/import-benefits")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferBenefitDto>>> ImportBenefits(
        Guid id, CancellationToken ct)
    {
        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId   == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        var imported = await _service.ImportBenefitsFromPositionAsync(id, tenantId.Value, employeeId.Value, ct);
        return Ok(imported);
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    public async Task<ActionResult<JobOfferNoteDto>> AddNote(Guid id, [FromBody] CreateJobOfferNoteDto dto)
    {
        dto.JobOfferId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var note = await _service.AddNoteAsync(dto, tenantId.Value, employeeId.Value);
        return Ok(note);
    }

    [HttpGet("{id:guid}/notes")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public async Task<ActionResult<IEnumerable<JobOfferNoteDto>>> GetNotes(Guid id)
        => Ok(await _service.GetNotesAsync(id));

    // =========================================================================
    // FILE UPLOADS
    // =========================================================================

    /// <summary>Upload or replace the offer letter PDF/DOCX.</summary>
    [HttpPost("{id:guid}/upload-letter")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> UploadLetter(Guid id, IFormFile file, CancellationToken ct)
        => UploadLetterAsync(id, file, signed: false, ct);

    /// <summary>Upload the signed offer letter returned by the candidate.</summary>
    [HttpPost("{id:guid}/upload-signed-letter")]
    [Authorize(Policy = HrPermissions.RecruitmentWritePolicy)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> UploadSignedLetter(Guid id, IFormFile file, CancellationToken ct)
        => UploadLetterAsync(id, file, signed: true, ct);

    private async Task<IActionResult> UploadLetterAsync(
        Guid id, IFormFile file, bool signed, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (_currentUser.TenantId is not Guid tenantId ||
            !Guid.TryParse(_currentUser.UserId, out var actorUserId))
            return BadRequest(new { message = "User context could not be resolved." });

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                ActorName = _currentUser.UserName,
                Category = ControlledFileUploadCategories.HrOfferLetters,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = signed
                        ? "Countersigned offer letter"
                        : "Issued offer letter",
                    SourceEntityType = "JobOffer",
                    SourceRecordId = id,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = signed ? "SignedOfferLetter" : "OfferLetter"
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            if (signed)
            {
                await _service.RecordSignedLetterAsync(
                    id, document.FileUploadRecordId,
                    document.DocumentRecordId, document.DocumentVersionId, ct);
            }
            else
            {
                await _service.RecordOfferLetterAsync(
                    id, document.FileUploadRecordId,
                    document.DocumentRecordId, document.DocumentVersionId, ct);
            }
        }
        catch
        {
            await _hrDocuments.RollbackAsync(document, tenantId, actorUserId, ct);
            throw;
        }

        // Offer letters are private: the response carries the download route, not a public URL.
        return Ok(new
        {
            downloadUrl = Url.Action(
                signed ? nameof(DownloadSignedLetter) : nameof(DownloadLetter),
                new { id })
        });
    }

    /// <summary>Streams the issued offer letter.</summary>
    [HttpGet("{id:guid}/letter")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public Task<IActionResult> DownloadLetter(Guid id, CancellationToken ct = default)
        => DownloadLetterAsync(id, signed: false, ct);

    /// <summary>Streams the countersigned offer letter.</summary>
    [HttpGet("{id:guid}/signed-letter")]
    [Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
    public Task<IActionResult> DownloadSignedLetter(Guid id, CancellationToken ct = default)
        => DownloadLetterAsync(id, signed: true, ct);

    private async Task<IActionResult> DownloadLetterAsync(
        Guid id, bool signed, CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest(new { message = "Tenant context could not be resolved." });

        var offer = await _db.Set<JobOffer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (offer is null)
            return NotFound();

        var uploadId = signed
            ? offer.SignedOfferLetterFileUploadRecordId
            : offer.OfferLetterFileUploadRecordId;
        var recordId = signed
            ? offer.SignedOfferLetterDocumentRecordId
            : offer.OfferLetterDocumentRecordId;
        var versionId = signed
            ? offer.SignedOfferLetterDocumentVersionId
            : offer.OfferLetterDocumentVersionId;
        var legacyPath = signed ? offer.SignedOfferLetterPath : offer.OfferLetterPath;

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            recordId, versionId, uploadId, legacyPath,
            fallbackFileName: signed
                ? $"signed-offer-{offer.OfferNumber}.pdf"
                : $"offer-{offer.OfferNumber}.pdf",
            fallbackContentType: "application/pdf",
            inline: false, ct);
    }
}
