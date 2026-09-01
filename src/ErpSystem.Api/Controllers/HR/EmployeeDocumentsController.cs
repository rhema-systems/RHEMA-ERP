using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The employee document file (Employee Master feedback: "no employee document attachments" and
/// "no mandatory documents against a position").
/// </summary>
/// <remarks>
/// <para><b>Its own controller rather than more routes on <c>EmployeesController</c>.</b> That file
/// already carries 81 write endpoints and reads as 73 of them unwired because its sub-resources go
/// through a path-builder helper. A gated upload needs a multipart action, a token-bearing download
/// and a vocabulary of its own; bolting three shapes onto the largest controller in the module
/// would make both harder to read. Same call as <c>api/succession-documents</c>.</para>
///
/// <para><b>⚠ There is no JSON create.</b> A document row exists only as the result of
/// <c>POST {employeeId}/upload</c>, which runs the virus-scanning gate and registers the file in the
/// central DMS. The metadata arrives as form fields beside the file and the three DMS ids are set by
/// the gate. A caller may say what a file IS and may never say where it lives — the sink that D-10,
/// D-14 and D-39 each had to remove after it had already shipped.</para>
/// </remarks>
[ApiController]
[Route("api/hr/employee-documents")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeDocumentsController : ControllerBase
{
    private readonly IEmployeeDocumentService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EmployeeDocumentsController> _logger;

    public EmployeeDocumentsController(
        IEmployeeDocumentService service,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        ApplicationDbContext db,
        ILogger<EmployeeDocumentsController> logger)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Surfaces a domain refusal with ITS OWN sentence.
    /// </summary>
    /// <remarks>
    /// ⚠ Without this every refusal on this controller reads "The operation is not valid for the
    /// current state of the object." <c>GlobalExceptionHandlingMiddleware</c> maps
    /// <c>InvalidOperationException</c> to a fixed string and DISCARDS the message, so a rule that
    /// fires correctly but cannot explain itself leaves the user staring at a disabled screen with
    /// no idea what to do. Caught by the harness, which asserts that a refusal says why — three of
    /// this slice's four failures were exactly this, and the code looked right in every one.
    /// The same helper, for the same reason, as <c>SeparationsController.ToClientError</c>.
    /// </remarks>
    private ActionResult ToClientError(Exception ex) => ex switch
    {
        ArgumentException => NotFound(new { message = ex.Message }),
        _ => BadRequest(new { message = ex.Message }),
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  TYPES — the vocabulary
    // ═══════════════════════════════════════════════════════════════════════

    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<EmployeeDocumentTypeDto>>> GetTypes(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _service.GetTypesAsync(includeInactive, ct));

    /// <summary>
    /// Seeds a starting vocabulary. Safe to run twice — existing names are skipped.
    /// </summary>
    /// <remarks>
    /// ⚠ A starting list, not TDC's answer. Without it the feature is unusable on day one: nothing
    /// can be uploaded until a type exists, and asking HR to invent the whole vocabulary before
    /// filing a single contract is how a feature goes unused.
    /// </remarks>
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpPost("types/seed-defaults")]
    public async Task<IActionResult> SeedDefaultTypes(CancellationToken ct = default)
        => Ok(new { added = await _service.SeedDefaultTypesAsync(ct) });

    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpPost("types")]
    public async Task<ActionResult<EmployeeDocumentTypeDto>> CreateType(
        [FromBody] CreateEmployeeDocumentTypeDto dto, CancellationToken ct = default)
    {
        try { return Ok(await _service.CreateTypeAsync(dto, ct)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ToClientError(ex); }
    }

    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<EmployeeDocumentTypeDto>> UpdateType(
        Guid id, [FromBody] UpdateEmployeeDocumentTypeDto dto, CancellationToken ct = default)
    {
        try { return Ok(await _service.UpdateTypeAsync(id, dto, ct)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ToClientError(ex); }
    }

    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteType(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _service.DeleteTypeAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ToClientError(ex); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DOCUMENTS
    // ═══════════════════════════════════════════════════════════════════════

    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeDocumentDto>>> GetForEmployee(
        Guid employeeId, CancellationToken ct = default)
        => Ok(await _service.GetForEmployeeAsync(employeeId, ct));

    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDocumentDto>> Get(Guid id, CancellationToken ct = default)
    {
        var document = await _service.GetAsync(id, ct);
        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>
    /// Uploads a document onto an employee's file, through the scanning gate and into the DMS.
    /// </summary>
    /// <remarks>
    /// ⚠ The employee is resolved BEFORE anything is stored: neither the gate nor the DMS performs
    /// an entitlement check, so the caller's right to touch the parent has to be established first.
    /// </remarks>
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [HttpPost("employee/{employeeId:guid}/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(
        Guid employeeId,
        IFormFile? file,
        [FromForm] Guid documentTypeId,
        [FromForm] string? title = null,
        [FromForm] string? description = null,
        [FromForm] DateOnly? issuedOn = null,
        [FromForm] DateOnly? expiresOn = null,
        CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid) return BadRequest("Tenant context could not be resolved.");

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: nameof(Core.Entities.HR.EmployeeDocument),
            sourceRecordId: employeeId,
            sourceLabel: "Employee document",
            documentType: "EmployeeDocument",
            description: description,
            persist: (_, document) => _service.AttachAsync(
                employeeId,
                documentTypeId,
                title,
                description,
                issuedOn,
                expiresOn,
                document.FileUploadRecordId,
                document.DocumentRecordId,
                document.DocumentVersionId,
                document.OriginalFileName,
                document.ContentType,
                document.FileSize,
                // ⚠ From the token, never from the form. Five earlier instances of the D-05 shape
                // were exactly an actor a caller could assert.
                _currentUser.EmployeeId,
                ct),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrEmployeeDocuments);
    }

    /// <summary>Streams the file back, byte-for-byte, to a caller who may read the employee.</summary>
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var document = await _service.GetEntityAsync(id, ct);
        if (document is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId,
            // No legacy path to fall back to: this table never had a free-text location, which is
            // the whole reason it was built after the gate rather than before it.
            legacyPath: null,
            document.FileName ?? "employee-document",
            fallbackContentType: document.MimeType,
            inline: false, ct);
    }

    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDocumentDto>> Update(
        Guid id, [FromBody] UpdateEmployeeDocumentDto dto, CancellationToken ct = default)
    {
        try { return Ok(await _service.UpdateAsync(id, dto, ct)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ToClientError(ex); }
    }

    /// <summary>
    /// Removes a document from the file. Admin tier.
    /// </summary>
    /// <remarks>
    /// ⚠ A higher bar than uploading, deliberately. These are the documents that evidence a
    /// person's identity and right to work; whoever can add one should not silently be able to
    /// remove the evidence that they did.
    /// </remarks>
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  POSITION REQUIREMENTS AND COMPLIANCE
    // ═══════════════════════════════════════════════════════════════════════

    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [HttpGet("positions/{positionId:guid}/requirements")]
    public async Task<ActionResult<IEnumerable<PositionDocumentRequirementDto>>> GetRequirements(
        Guid positionId, CancellationToken ct = default)
        => Ok(await _service.GetRequirementsAsync(positionId, ct));

    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpPost("requirements")]
    public async Task<ActionResult<PositionDocumentRequirementDto>> AddRequirement(
        [FromBody] CreatePositionDocumentRequirementDto dto, CancellationToken ct = default)
    {
        try { return Ok(await _service.AddRequirementAsync(dto, ct)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ToClientError(ex); }
    }

    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpPut("requirements/{id:guid}")]
    public async Task<ActionResult<PositionDocumentRequirementDto>> UpdateRequirement(
        Guid id, [FromBody] UpdatePositionDocumentRequirementDto dto, CancellationToken ct = default)
        => Ok(await _service.UpdateRequirementAsync(id, dto, ct));

    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [HttpDelete("requirements/{id:guid}")]
    public async Task<IActionResult> DeleteRequirement(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteRequirementAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// What this employee's position requires and whether they hold it — the read that makes the
    /// requirement register a control rather than a filing preference.
    /// </summary>
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [HttpGet("employee/{employeeId:guid}/compliance")]
    public async Task<ActionResult<EmployeeDocumentComplianceDto>> GetCompliance(
        Guid employeeId, CancellationToken ct = default)
    {
        try { return Ok(await _service.GetComplianceAsync(employeeId, ct)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ToClientError(ex); }
    }
}
