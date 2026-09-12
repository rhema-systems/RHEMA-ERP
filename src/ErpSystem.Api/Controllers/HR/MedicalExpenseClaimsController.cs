using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
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

[ApiController]
[Route("api/medical-expense-claims")]
// Medical records are special-category personal data. This controller previously carried a
// bare [Authorize], so any authenticated employee could read them. Read is the class-level
// floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class MedicalExpenseClaimsController : MedicalControllerBase
{
    private readonly IMedicalExpenseClaimService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;

    public MedicalExpenseClaimsController(
        IMedicalExpenseClaimService service,
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MedicalExpenseClaimDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimByIdAsync(id, ct));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<MedicalExpenseClaimDetailDto>> GetWithDetails(Guid id, CancellationToken ct)
        => Ok(await _service.GetClaimWithDetailsAsync(id, ct));

    [HttpGet("number/{claimNumber}")]
    public async Task<ActionResult<MedicalExpenseClaimDto?>> GetByNumber(string claimNumber, CancellationToken ct)
        => Ok(await _service.GetClaimByNumberAsync(claimNumber, ct));

    [HttpGet("employees/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimSummaryDto>>> GetByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetClaimsByEmployeeAsync(employeeId, ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<MedicalExpenseClaimSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ClaimStatus? status = null,
        CancellationToken ct = default)
        => Ok(await _service.GetClaimsPagedAsync(pageNumber, pageSize, status, ct));

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimSummaryDto>>> GetPending(CancellationToken ct)
        => Ok(await _service.GetPendingClaimsAsync(ct));

    [HttpGet("flagged")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimSummaryDto>>> GetFlagged(CancellationToken ct)
        => Ok(await _service.GetFlaggedClaimsAsync(ct));

    /// <summary>
    /// Files a medical expense claim on behalf of an employee. HR-facing.
    /// </summary>
    /// <remarks>
    /// <para><c>EmployeeId</c> is required here: this is the HR caseload surface, and everything on
    /// it already requires the medical write permission, which an ordinary employee does not hold.
    /// Employees file their <b>own</b> claims through the self-service surface instead.</para>
    ///
    /// <para>This previously defaulted <c>EmployeeId</c> to the caller and carried a role check to
    /// allow HR to override it — a branch no ordinary employee could ever reach, because the
    /// permission gate above had already turned them away. It read like a working self-service
    /// path and was not one.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<MedicalExpenseClaimDto>> Create(
        [FromBody] CreateMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.EmployeeId is null || dto.EmployeeId == Guid.Empty)
            return BadRequest("An employee id is required. Employees file their own claims through self-service.");
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateClaimAsync(dto, dto.EmployeeId.Value, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MedicalExpenseClaimDto>> Update(
        Guid id,
        [FromBody] UpdateMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateClaimAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/approval")]
    public async Task<IActionResult> ProcessApproval(
        Guid id,
        [FromBody] ProcessMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        // ApprovedById is an Employee FK, so an adjudicator genuinely has to be an employee —
        // unlike filing, where the audit field takes the user id. Say which it is.
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Recording a claim approval") is { } error) return error;

        await _service.ProcessApprovalAsync(dto, tenantId, employeeId, userId, ct);
        return Ok(new { message = "Approval recorded." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> ProcessPayment(
        Guid id,
        [FromBody] ProcessMedicalExpensePaymentDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.ProcessPaymentAsync(dto, userId, ct);
        return Ok(new { message = "Payment processed." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/flag")]
    public async Task<IActionResult> Flag(
        Guid id,
        [FromBody] FlagMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.FlagClaimAsync(dto, userId, ct);
        return Ok(new { message = "Claim flagged." });
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{id:guid}/unflag")]
    public async Task<IActionResult> Unflag(
        Guid id,
        [FromBody] UnflagMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.UnflagClaimAsync(dto, userId, ct);
        return Ok(new { message = "Claim unflagged." });
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteClaimAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // ITEMS
    // =========================================================================

    [HttpGet("{claimId:guid}/items")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseItemDto>>> GetItems(
        Guid claimId,
        CancellationToken ct)
        => Ok(await _service.GetItemsAsync(claimId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{claimId:guid}/items")]
    public async Task<ActionResult<MedicalExpenseItemDto>> AddItem(
        Guid claimId,
        [FromBody] CreateMedicalExpenseItemDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = claimId;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddItemAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = claimId }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult<MedicalExpenseItemDto>> UpdateItem(
        Guid id,
        [FromBody] UpdateMedicalExpenseItemDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateItemAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        await _service.DeleteItemAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // DOCUMENTS
    // =========================================================================

    [HttpGet("{claimId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseDocumentDto>>> GetDocuments(
        Guid claimId,
        CancellationToken ct)
        => Ok(await _service.GetDocumentsAsync(claimId, ct));

    /// <summary>Uploads a supporting document — typically a receipt — against a claim.</summary>
    /// <remarks>
    /// This used to be a JSON endpoint taking a caller-supplied <c>FilePath</c>, which let any
    /// authenticated user with write access attach arbitrary bytes on disk — including another
    /// tenant's — to a claim. It is now a real multipart upload routed through the shared gate, so
    /// the file is malware-scanned and stored outside the publicly served web root. Same fix, same
    /// shape, as medical exam documents.
    /// </remarks>
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{claimId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(MedicalExpenseDocumentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDocument(
        Guid claimId,
        IFormFile file,
        [FromForm] MedicalDocumentType type,
        [FromForm] string? description,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var claim = await LoadClaimInTenantAsync(claimId, tenantId, ct);
        if (claim is null) return NotFound("Medical expense claim not found.");

        return await MedicalClaimDocumentUpload.ExecuteAsync(
            this, _hrDocuments, _service, claimId, file, type, description,
            tenantId, userId, CurrentUser.UserName, ct);
    }

    /// <summary>Streams a document attached to a claim.</summary>
    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var document = await _db.Set<MedicalExpenseDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (document is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    /// <summary>Confirms a claim exists in this tenant before anything is stored against it.</summary>
    private Task<MedicalExpenseClaim?> LoadClaimInTenantAsync(Guid claimId, Guid tenantId, CancellationToken ct)
        => _db.Set<MedicalExpenseClaim>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == claimId && item.TenantId == tenantId && !item.IsDeleted, ct);

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        await _service.DeleteDocumentAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // NOTES
    // =========================================================================

    [HttpGet("{claimId:guid}/notes")]
    public async Task<ActionResult<IEnumerable<MedicalExpenseClaimNoteDto>>> GetNotes(
        Guid claimId,
        [FromQuery] bool internalOnly = false,
        CancellationToken ct = default)
        => Ok(internalOnly
            ? await _service.GetInternalNotesAsync(claimId, ct)
            : await _service.GetNotesAsync(claimId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("{claimId:guid}/notes")]
    public async Task<ActionResult<MedicalExpenseClaimNoteDto>> AddNote(
        Guid claimId,
        [FromBody] AddMedicalExpenseClaimNoteDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = claimId;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // AuthorId is an Employee FK — a note has to be attributable to a person, not an account.
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Adding a note to a claim") is { } error) return error;

        var created = await _service.AddNoteAsync(dto, tenantId, employeeId, userId, ct);
        return Ok(created);
    }
}
