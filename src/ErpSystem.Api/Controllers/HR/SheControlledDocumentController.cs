using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// SHE controlled document register (SRS §14 / FR-SHE-246, FR-SHE-170):
/// filing/retrieval plus version control — revisions through the shared
/// controlled-upload gate onto the central DMS, approval via activate,
/// effective dates and full document history. HR-gated end to end — the
/// register is a governance surface, not employee self-service.
/// </summary>
[ApiController]
[Route("api/safety/documents")]
[SafetyBusinessRules]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
public class SheControlledDocumentController : SheApiControllerBase
{
    private readonly ISheControlledDocumentService _service;

    public SheControlledDocumentController(ISheControlledDocumentService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── register (FR-SHE-170 filing & retrieval) ──
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheControlledDocumentSummaryDto>>> GetAll(
        [FromQuery] SheControlledDocumentCategory? category,
        [FromQuery] SheControlledDocumentStatus? status,
        [FromQuery] string? search,
        [FromQuery] int? dueForReviewInDays)
        => Ok(await _service.GetAllAsync(category, status, search, dueForReviewInDays));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheControlledDocumentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpPost]
    public async Task<ActionResult<SheControlledDocumentDto>> Create([FromBody] CreateSheControlledDocumentDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SheControlledDocumentDto>> Update(Guid id, [FromBody] UpdateSheControlledDocumentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    /// <summary>Drafts without history only — a document with versions archives, never deletes.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── version control (FR-SHE-246) ──
    /// <summary>
    /// Uploads the next revision through the controlled-upload gate. Refused with
    /// the gate's own {code, message} contract for rejected files (type, size,
    /// quota, scan), and 422 on an archived document.
    /// </summary>
    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<SheControlledDocumentDto>> UploadVersion(
        Guid id, IFormFile? file, [FromForm] string? changeSummary, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (!Guid.TryParse(CurrentUser.UserId, out var actorUserId))
            return Unauthorized("User context could not be resolved");

        try
        {
            var updated = await _service.UploadVersionAsync(new SheControlledDocumentVersionUpload
            {
                DocumentId = id,
                ActorUserId = actorUserId,
                ActorName = CurrentUser.UserName,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                OpenReadStream = file.OpenReadStream,
                ChangeSummary = changeSummary,
            }, UserId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, updated);
        }
        catch (ControlledFileUploadException ex)
        {
            // The gate's own {code, message} contract — a refused file is not a server fault.
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    /// <summary>Streams a version's bytes; refuses anything without a clean scan verdict.</summary>
    [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> DownloadVersion(
        Guid id, Guid versionId,
        [FromServices] ICentralDocumentRepositoryFileService centralDocuments,
        [FromServices] IFileStorageService storage,
        [FromServices] ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        // Resolves the version against the register row (tenant + record match)
        // before anything is served — that is this endpoint's entitlement check.
        var fileInfo = await _service.GetVersionFileAsync(id, versionId, cancellationToken);

        return await HrDocumentDownload.ServeAsync(
            this, centralDocuments, storage, db,
            TenantId,
            fileInfo.DocumentRecordId,
            fileInfo.VersionId,
            fileInfo.FileUploadRecordId,
            legacyPath: null,
            fallbackFileName: fileInfo.FileName,
            fallbackContentType: fileInfo.ContentType,
            inline: false,
            cancellationToken);
    }

    // ── lifecycle (FR-SHE-246 approval workflow) ──
    /// <summary>The approval step — refused (422) while the document has no uploaded version.</summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<SheControlledDocumentDto>> Activate(Guid id, [FromBody] ActivateSheControlledDocumentDto dto)
    {
        dto.DocumentId = id;
        return Ok(await _service.ActivateAsync(dto, UserId));
    }

    [HttpPost("{id:guid}/start-review")]
    public async Task<ActionResult<SheControlledDocumentDto>> StartReview(Guid id)
        => Ok(await _service.StartReviewAsync(id, UserId));

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<SheControlledDocumentDto>> Archive(Guid id, [FromBody] ArchiveSheControlledDocumentDto dto)
    {
        dto.DocumentId = id;
        return Ok(await _service.ArchiveAsync(dto, UserId));
    }
}
