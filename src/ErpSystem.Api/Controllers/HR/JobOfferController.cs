using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/job-offers")]
[Authorize]
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
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetAll()
        => Ok(await _service.GetAllSummaryAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobOfferDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{offerNumber}")]
    public async Task<ActionResult<JobOfferDto?>> GetByOfferNumber(string offerNumber)
        => Ok(await _service.GetByOfferNumberAsync(offerNumber));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<JobOfferDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("application/{applicationId:guid}")]
    public async Task<ActionResult<JobOfferDto?>> GetByApplication(Guid applicationId)
        => Ok(await _service.GetByApplicationIdAsync(applicationId));

    /// <summary>
    /// Generates the formal offer-of-employment letter for internal preview / print-to-PDF. The
    /// letter is rendered from the HR-editable "OfferLetter" template enriched with the offer terms,
    /// job-description summary + duties, itemised salary breakdown, benefits and pre-employment
    /// conditions.
    /// </summary>
    [HttpGet("{id:guid}/letter")]
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
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetByStatus(JobOfferStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetExpiring(
        [FromQuery] int daysAhead = 7)
        => Ok(await _service.GetExpiringOffersAsync(daysAhead));

    [HttpGet("prepared-by/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<JobOfferSummaryDto>>> GetByPreparedBy(Guid employeeId)
        => Ok(await _service.GetByPreparedByAsync(employeeId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
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
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/submit-for-approval")]
    public async Task<IActionResult> SubmitForApproval(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.SubmitForApprovalAsync(id);
        return Ok(new { message = "Offer submitted for approval." });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveJobOfferDto dto)
    {
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
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssueJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.IssueAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer issued." });
    }

    [HttpPost("{id:guid}/record-response")]
    public async Task<IActionResult> RecordResponse(Guid id, [FromBody] RecordOfferResponseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordResponseAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer response recorded." });
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeJobOfferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RevokeAsync(dto, employeeId.Value);
        return Ok(new { message = "Offer revoked." });
    }

    [HttpPost("{id:guid}/accept-conditionally")]
    public async Task<ActionResult<JobOfferDto>> AcceptConditionally(Guid id, [FromBody] string? candidateResponseNotes = null)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var result = await _service.AcceptConditionallyAsync(id, employeeId.Value, candidateResponseNotes);
        return Ok(result);
    }

    [HttpPost("{id:guid}/revise")]
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

    /// <summary>Preview benefits from the position grade without persisting them.</summary>
    [HttpGet("{id:guid}/suggest-benefits")]
    public async Task<ActionResult<IEnumerable<JobOfferBenefitDto>>> SuggestBenefits(
        Guid id, CancellationToken ct)
        => Ok(await _service.SuggestBenefitsFromPositionAsync(id, ct));

    /// <summary>Import position-grade benefits into the offer (deduped, persists to DB).</summary>
    [HttpPost("{id:guid}/import-benefits")]
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
    public async Task<ActionResult<JobOfferNoteDto>> AddNote(Guid id, [FromBody] CreateJobOfferNoteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (id != dto.JobOfferId) return BadRequest("ID mismatch.");

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
    public async Task<ActionResult<IEnumerable<JobOfferNoteDto>>> GetNotes(Guid id)
        => Ok(await _service.GetNotesAsync(id));

    // =========================================================================
    // FILE UPLOADS
    // =========================================================================

    /// <summary>Upload or replace the offer letter PDF/DOCX.</summary>
    [HttpPost("{id:guid}/upload-letter")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> UploadLetter(Guid id, IFormFile file, CancellationToken ct)
        => UploadLetterAsync(id, file, signed: false, ct);

    /// <summary>Upload the signed offer letter returned by the candidate.</summary>
    [HttpPost("{id:guid}/upload-signed-letter")]
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
    public Task<IActionResult> DownloadLetter(Guid id, CancellationToken ct = default)
        => DownloadLetterAsync(id, signed: false, ct);

    /// <summary>Streams the countersigned offer letter.</summary>
    [HttpGet("{id:guid}/signed-letter")]
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
