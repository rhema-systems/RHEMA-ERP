using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
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
/// Oaths of secrecy (FRD FR-HR-030: "The system shall record an oath of secrecy as part of
/// onboarding", priority M).
/// </summary>
/// <remarks>
/// <para>Two paths that must not be confused. <b>Affirm</b> is the employee's own act: no employee
/// id on the payload, the actor read from the token, the date stamped by the server, and an IP and
/// tamper hash recorded. <b>Administered</b> is HR keying in a paper oath afterwards, which needs a
/// named witness and carries no signature — because the attestation there is the witness and the
/// scan, not somebody clicking.</para>
///
/// <para>⚠ HR cannot affirm on an employee's behalf, and there is no endpoint that would allow it.
/// Signing "I swear" in another person's name, with their IP recorded, is precisely what this
/// record exists to make impossible — the same rule area 8 applies to accepting a movement and area
/// 9 to acknowledging a disciplinary decision.</para>
/// </remarks>
[ApiController]
[Route("api/hr/oaths-of-secrecy")]
[Authorize(Policy = "InternalOnly")] // W3 backstop: ANDed with the per-action gates below
public class OathsOfSecrecyController : ControllerBase
{
    private readonly IEmployeeOathOfSecrecyService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OathsOfSecrecyController> _logger;

    public OathsOfSecrecyController(
        IEmployeeOathOfSecrecyService service,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ILogger<OathsOfSecrecyController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _logger = logger;
    }

    private Guid RequireEmployeeId()
        => _currentUser.EmployeeId
           ?? throw new UnauthorizedAccessException(
               "Your user account is not linked to an employee record. Please contact your administrator.");

    /// <summary>Every oath on record for one employee, newest first.</summary>
    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeOathOfSecrecyDto>>> GetForEmployee(Guid employeeId)
        => Ok(await _service.GetForEmployeeAsync(employeeId));

    /// <summary>The caller's own oaths. Token-derived — no id to point elsewhere.</summary>
    [Authorize(Policy = "InternalOnly")]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<EmployeeOathOfSecrecyDto>>> GetMine()
        => Ok(await _service.GetForEmployeeAsync(RequireEmployeeId()));

    /// <summary>Active employees with no oath on record.</summary>
    /// <remarks>
    /// The view that turns FR-HR-030 from a table into a requirement anyone can act on: a register
    /// of oaths nobody can query for gaps records nothing useful.
    /// </remarks>
    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("outstanding")]
    public async Task<ActionResult<IEnumerable<OathOutstandingEmployeeDto>>> GetOutstanding()
        => Ok(await _service.GetOutstandingAsync());

    /// <summary>The employee affirms their own oath.</summary>
    [Authorize(Policy = "InternalOnly")]
    [HttpPost("affirm")]
    public async Task<ActionResult<EmployeeOathOfSecrecyDto>> Affirm([FromBody] AffirmOathOfSecrecyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(await _service.AffirmAsync(dto, RequireEmployeeId(), ip));
    }

    /// <summary>HR records an oath sworn on paper before a witness.</summary>
    [Authorize(Policy = HrPermissions.ProbationWritePolicy)]
    [HttpPost("administered")]
    public async Task<ActionResult<EmployeeOathOfSecrecyDto>> RecordAdministered(
        [FromBody] RecordAdministeredOathDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.RecordAdministeredAsync(dto, RequireEmployeeId()));
    }

    /// <summary>Attaches the scanned signed copy to an oath.</summary>
    /// <remarks>
    /// <para>Runs the controlled (virus-scanned) upload gate and registers the file in the central
    /// DMS, exactly as travel and medical attachments do. ⚠ The oath is read first: neither the gate
    /// nor the DMS performs an entitlement check, so the caller's right to touch the parent has to
    /// be established <b>before</b> anything is stored.</para>
    ///
    /// <para>There is no way to supply a file path or an upload id on the JSON write paths — this
    /// endpoint is the only route by which a scan reaches an oath.</para>
    /// </remarks>
    [Authorize(Policy = HrPermissions.ProbationWritePolicy)]
    [HttpPost("{oathId:guid}/scan")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> AttachScan(
        Guid oathId,
        IFormFile? file,
        [FromForm] string? description = null,
        CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        // Entitlement first, storage second. Reading the oath through the service applies the
        // tenant check and 404s an id that is not ours.
        var oath = await _db.Set<Core.Entities.HR.Recruitment.EmployeeOathOfSecrecy>()
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == oathId && o.TenantId == tenantId && !o.IsDeleted, ct);
        if (oath is null) return NotFound(new { message = $"Oath '{oathId}' was not found." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: nameof(Core.Entities.HR.Recruitment.EmployeeOathOfSecrecy),
            sourceRecordId: oathId,
            sourceLabel: "Oath of secrecy",
            documentType: "OathOfSecrecy",
            description: description,
            persist: (_, document) => _service.AttachScanAsync(
                oathId,
                document.FileUploadRecordId,
                document.DocumentRecordId,
                document.DocumentVersionId,
                document.OriginalFileName,
                document.ContentType,
                document.FileSize,
                ct),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrOathOfSecrecyDocuments);
    }

    /// <summary>Streams the signed scan back, byte-for-byte.</summary>
    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("{oathId:guid}/scan")]
    public async Task<IActionResult> DownloadScan(Guid oathId, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var oath = await _db.Set<Core.Entities.HR.Recruitment.EmployeeOathOfSecrecy>()
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == oathId && o.TenantId == tenantId && !o.IsDeleted, ct);
        if (oath is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            oath.DocumentRecordId, oath.DocumentVersionId,
            oath.FileUploadRecordId,
            // No legacy path: this record never had a free-text location to fall back to, which is
            // the point of adding it after the controlled gate existed rather than before.
            legacyPath: null,
            oath.FileName ?? "oath-of-secrecy",
            fallbackContentType: oath.MimeType,
            inline: false, ct);
    }
}
