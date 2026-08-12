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

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Pre-employment checks against a conditional offer — medical, police clearance, background,
/// academic and professional verification, references, credit and drug testing.
///
/// <para><b>HR-only, reads included, and the most sensitive data in the module:</b> criminal-record
/// results, medical outcomes and referees' candid opinions about a named person. This controller
/// previously carried a bare <c>[Authorize]</c>, so any authenticated employee could read all of
/// it and record results against anyone.</para>
///
/// <para>⚠ Completing a check is gated: mandatory and blocking items must have a recorded outcome
/// first. Waived and not-applicable items count as settled, not as failures.</para>
/// </summary>
[ApiController]
[Route("api/pre-employment-checks")]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
[RecruitmentBusinessRules]
public class PreEmploymentCheckController : ControllerBase
{
    private readonly IPreEmploymentCheckService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PreEmploymentCheckController> _logger;

    public PreEmploymentCheckController(
        IPreEmploymentCheckService service,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ILogger<PreEmploymentCheckController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _logger = logger;
    }

    // =========================================================================
    // CHECK QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PreEmploymentCheckDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("offer/{offerId:guid}")]
    public async Task<ActionResult<PreEmploymentCheckDto?>> GetByOffer(Guid offerId)
        => Ok(await _service.GetByOfferIdAsync(offerId));

    [HttpGet("{id:guid}/with-items")]
    public async Task<ActionResult<PreEmploymentCheckDetailDto>> GetWithItems(Guid id)
        => Ok(await _service.GetWithItemsAsync(id));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckDto>>> GetByStatus(
        PreEmploymentCheckStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    // =========================================================================
    // CHECK CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<PreEmploymentCheckDto>> Create([FromBody] CreatePreEmploymentCheckDto dto)
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

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteCheck(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CompleteCheckAsync(id, employeeId.Value);
        return Ok(new { message = "Pre-employment check completed." });
    }

    // =========================================================================
    // CHECK ITEMS
    // =========================================================================

    [HttpGet("{checkId:guid}/items")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetItems(Guid checkId)
        => Ok(await _service.GetItemsAsync(checkId));

    [HttpGet("{checkId:guid}/items/blocking-failures")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetBlockingFailures(Guid checkId)
        => Ok(await _service.GetBlockingFailuresAsync(checkId));

    [HttpGet("{checkId:guid}/items/mandatory")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetMandatoryItems(Guid checkId)
        => Ok(await _service.GetMandatoryItemsAsync(checkId));

    [HttpGet("items/status/{status}")]
    public async Task<ActionResult<IEnumerable<PreEmploymentCheckItemDto>>> GetItemsByStatus(
        CheckItemStatus status, [FromQuery] Guid? checkId = null)
        => Ok(await _service.GetItemsByStatusAsync(status, checkId));

    [HttpPost("{checkId:guid}/items")]
    public async Task<ActionResult<PreEmploymentCheckItemDto>> AddItem(
        Guid checkId, [FromBody] CreatePreEmploymentCheckItemDto dto)
    {
        dto.PreEmploymentCheckId = checkId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddItemAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<PreEmploymentCheckItemDto>> UpdateItem(
        Guid itemId, [FromBody] UpdatePreEmploymentCheckItemDto dto)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateItemAsync(dto, employeeId.Value));
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        await _service.DeleteItemAsync(itemId);
        return NoContent();
    }

    // =========================================================================
    // REFERENCE RESPONSES
    // =========================================================================

    [HttpGet("items/{checkItemId:guid}/reference-responses")]
    public async Task<ActionResult<IEnumerable<ReferenceCheckResponseDto>>> GetReferenceResponses(
        Guid checkItemId)
        => Ok(await _service.GetReferenceResponsesAsync(checkItemId));

    [HttpGet("reference-responses/referee/{refereeId:guid}")]
    public async Task<ActionResult<IEnumerable<ReferenceCheckResponseDto>>> GetReferenceResponsesByReferee(
        Guid refereeId)
        => Ok(await _service.GetReferenceResponsesByRefereeAsync(refereeId));

    [HttpPost("items/{checkItemId:guid}/reference-responses")]
    public async Task<ActionResult<ReferenceCheckResponseDto>> AddReferenceResponse(
        Guid checkItemId, [FromBody] CreateReferenceCheckResponseDto dto)
    {
        dto.CheckItemId = checkItemId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddReferenceResponseAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("reference-responses/{id:guid}")]
    public async Task<ActionResult<ReferenceCheckResponseDto>> UpdateReferenceResponse(
        Guid id, [FromBody] UpdateReferenceCheckResponseDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateReferenceResponseAsync(dto, employeeId.Value));
    }

    [HttpDelete("reference-responses/{id:guid}")]
    public async Task<IActionResult> DeleteReferenceResponse(Guid id)
    {
        await _service.DeleteReferenceResponseAsync(id);
        return NoContent();
    }

    // =========================================================================
    // EVIDENCE DOCUMENTS
    // =========================================================================
    //
    // ⚠ Evidence goes through the controlled-upload gate — virus scan, then DMS registration —
    // exactly like requisition and appraisal attachments. It used to be a `documentPath` string on
    // the request payload, so a caller named any path they liked and nothing ever scanned or stored
    // a file. `HrPreEmploymentDocuments` is its own storage category because these are third-party
    // verification results about a named person: the most sensitive documents recruitment holds.

    /// <summary>Uploads the evidence behind one check item — a clearance certificate, a report.</summary>
    [HttpPost("items/{itemId:guid}/document")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<IActionResult> UploadItemDocument(Guid itemId, IFormFile file, CancellationToken ct)
        => HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "PreEmploymentCheckItem",
            sourceRecordId: itemId,
            sourceLabel: "Pre-employment check evidence",
            documentType: "PreEmploymentCheckEvidence",
            description: null,
            persist: (_, document) => _service.RecordItemDocumentAsync(
                itemId, document.FileUploadRecordId, document.DocumentRecordId,
                document.DocumentVersionId, Path.GetFileName(file.FileName), ct),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrPreEmploymentDocuments);

    /// <summary>Streams the evidence behind a check item.</summary>
    [HttpGet("items/{itemId:guid}/document")]
    public async Task<IActionResult> DownloadItemDocument(Guid itemId, CancellationToken ct)
    {
        var handle = await _service.GetItemDocumentHandleAsync(itemId, ct);
        return await ServeDocumentAsync(handle, ct);
    }

    /// <summary>Uploads a written reference returned by a referee.</summary>
    [HttpPost("reference-responses/{responseId:guid}/document")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<IActionResult> UploadReferenceDocument(Guid responseId, IFormFile file, CancellationToken ct)
        => HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "ReferenceCheckResponse",
            sourceRecordId: responseId,
            sourceLabel: "Written reference",
            documentType: "ReferenceCheckResponse",
            description: null,
            persist: (_, document) => _service.RecordReferenceDocumentAsync(
                responseId, document.FileUploadRecordId, document.DocumentRecordId,
                document.DocumentVersionId, Path.GetFileName(file.FileName), ct),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrPreEmploymentDocuments);

    /// <summary>Streams a written reference.</summary>
    [HttpGet("reference-responses/{responseId:guid}/document")]
    public async Task<IActionResult> DownloadReferenceDocument(Guid responseId, CancellationToken ct)
    {
        var handle = await _service.GetReferenceDocumentHandleAsync(responseId, ct);
        return await ServeDocumentAsync(handle, ct);
    }

    /// <summary>
    /// Shared tail of both download routes. The service has already established that the record is
    /// in the caller's tenant — the DMS performs no entitlement check of its own.
    /// </summary>
    private async Task<IActionResult> ServeDocumentAsync(
        PreEmploymentDocumentHandleDto? handle, CancellationToken ct)
    {
        if (handle is null)
            return NotFound(new { message = "No document has been attached." });

        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest(new { message = "Tenant context could not be resolved." });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            handle.DocumentRecordId, handle.DocumentVersionId, handle.FileUploadRecordId,
            handle.LegacyPath,
            fallbackFileName: handle.FileName,
            fallbackContentType: "application/octet-stream",
            inline: false, ct);
    }

    // =========================================================================
    // TEMPLATE APPLICATION

    /// <summary>
    /// Apply a pre-employment check template to an existing check, seeding
    /// its items from the template. Existing items of the same CheckType are
    /// skipped unless overwriteExisting is true.
    /// </summary>
    [HttpPost("{id:guid}/apply-template")]
    public async Task<ActionResult<PreEmploymentCheckDetailDto>> ApplyTemplate(
        Guid id,
        [FromBody] ApplyTemplateDto dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return Unauthorized();

        var result = await _service.ApplyTemplateAsync(id, dto.TemplateId, employeeId.Value, dto.OverwriteExisting);
        return Ok(result);
    }
}
