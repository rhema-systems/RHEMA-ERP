using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/nhis-claims")]
// NHIS claims are employee-linked medical records — amounts, diagnoses and supporting documents.
// This controller was missed when the other medical controllers were re-gated and kept a bare
// [Authorize], so any authenticated employee could read, edit and delete every claim in the
// tenant. Read is the class-level floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class NHISClaimsController : MedicalControllerBase
{
    private readonly INHISService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;

    public NHISClaimsController(
        INHISService service,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
    }

    // =========================================================================
    // CLAIMS
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NHISClaimSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllClaimsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NHISClaimDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimByIdAsync(id, ct));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<NHISClaimDetailDto>> GetWithDocuments(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimWithDocumentsAsync(id, ct));

    [HttpGet("number/{claimNumber}")]
    public async Task<ActionResult<NHISClaimDto?>> GetByNumber(string claimNumber, CancellationToken ct)
        => Ok(await _service.GetClaimByNumberAsync(claimNumber, ct));

    [HttpGet("employees/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<NHISClaimSummaryDto>>> GetByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetClaimsByEmployeeAsync(employeeId, ct));

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<NHISClaimSummaryDto>>> GetPending(CancellationToken ct)
        => Ok(await _service.GetPendingClaimsAsync(ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<NHISClaimDto>> Create(
        [FromBody] CreateNHISClaimDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateClaimAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NHISClaimDto>> Update(
        Guid id,
        [FromBody] UpdateNHISClaimDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateClaimAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateNHISClaimStatusDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.UpdateClaimStatusAsync(dto, userId, ct);
        return Ok(new { message = "Claim status updated." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromBody] SubmitNHISClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.SubmitClaimAsync(dto, userId, ct);
        return Ok(new { message = "Claim submitted." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordNHISClaimPaymentDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.RecordClaimPaymentAsync(dto, userId, ct);
        return Ok(new { message = "Payment recorded." });
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteClaimAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{nhisClaimId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<NHISClaimDocumentDto>>> GetDocuments(
        Guid nhisClaimId,
        CancellationToken ct)
        => Ok(await _service.GetClaimDocumentsAsync(nhisClaimId, ct));

    /// <summary>Records a document against a claim. Metadata only - see the remarks.</summary>
    /// <remarks>
    /// D-14: this used to REQUIRE a caller-supplied <c>FilePath</c> and store it verbatim, which
    /// let any HR user point a document row at arbitrary bytes on disk. It was the fifth instance
    /// of that sink in the medical module. Files now arrive through <c>POST documents/upload</c>
    /// below; this route survives for the legacy migration utility and refuses every file-location
    /// field, so through the API it can only mint metadata.
    /// </remarks>
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("documents")]
    public async Task<ActionResult<NHISClaimDocumentDto>> AddDocument(
        [FromBody] CreateNHISClaimDocumentDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        if (!string.IsNullOrWhiteSpace(dto.FilePath) ||
            dto.FileUploadRecordId.HasValue ||
            dto.DocumentRecordId.HasValue ||
            dto.DocumentVersionId.HasValue)
        {
            return BadRequest(new
            {
                message = "File locations cannot be supplied directly. " +
                          "Use POST nhis-claims/documents/upload to attach a file."
            });
        }

        var created = await _service.AddClaimDocumentAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    /// <summary>Attaches a file to an NHIS claim through the controlled boundary.</summary>
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<NHISClaimDocumentDto>> UploadDocument(
        [FromForm] Guid nhisClaimId,
        [FromForm] IFormFile file,
        [FromForm] string? description = null,
        CancellationToken ct = default)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        // Refuse a claim from another tenant before any bytes are stored. AddClaimDocumentAsync
        // checks the tenant but never the claim, so without this the FK alone decides.
        var claimExists = await _db.Set<NHISClaim>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == nhisClaimId && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (!claimExists) return NotFound("That NHIS claim could not be found.");

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = userId,
                ActorName = CurrentUser.UserName,
                // Shares the medical-claim category rather than minting its own: an NHIS document
                // is the same artefact about the same person, read by the same permission family,
                // as a medical expense claim receipt. A separate category would split one
                // retention rule in two for no difference in content or audience.
                Category = ControlledFileUploadCategories.HrMedicalClaimDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "NHIS claim document",
                    SourceEntityType = nameof(NHISClaim),
                    SourceRecordId = nhisClaimId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = "NHISClaimDocument",
                    ChangeSummary = description
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            // The gate's own {code, message} contract - a refused file is not a server fault.
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var created = await _service.AddClaimDocumentAsync(
                new CreateNHISClaimDocumentDto
                {
                    NHISClaimId = nhisClaimId,
                    FileName = document.OriginalFileName,
                    FilePath = string.Empty,
                    FileUploadRecordId = document.FileUploadRecordId,
                    DocumentRecordId = document.DocumentRecordId,
                    DocumentVersionId = document.DocumentVersionId,
                    Description = description
                },
                tenantId, userId, ct);

            return Ok(created);
        }
        catch
        {
            // Leave no scanned-and-registered document behind pointing at a row never written.
            await _hrDocuments.RollbackAsync(document, tenantId, userId, ct);
            throw;
        }
    }

    /// <summary>Streams an NHIS claim document to a caller entitled to see it.</summary>
    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var document = await _db.Set<NHISClaimDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (document is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        await _service.DeleteClaimDocumentAsync(id, ct);
        return NoContent();
    }
}
