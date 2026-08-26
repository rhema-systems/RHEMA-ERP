using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The employee's own profile: what it says, the parts they may correct themselves, and the
/// requests for the parts only HR may change (area 25 slice 12, decision D6).
/// </summary>
/// <remarks>
/// <para>Sits beside <c>EmployeePortalController</c> under the same <c>api/employee-portal</c>
/// prefix rather than inside it — the portal controller is already the area's largest file,
/// and this surface has its own service and its own desk counterpart
/// (<c>api/hr/profile-change-requests</c>).</para>
///
/// <para>The scoping law is the module's: the employee comes from the token, <b>no route or
/// query parameter here ever carries an employee id</b>, and a request belonging to somebody
/// else is a lookup miss rather than a refusal — a 403 would confirm it exists.</para>
///
/// <para>Census row #39 ("my profile") was verdict M at slice 0: there was no self-shaped
/// profile endpoint at all, and the desk's <c>EmployeeFullProfileDto</c> could not be reused
/// because it carries salary, salary history and HR's private notes about the employee.
/// <see cref="MyProfileDto"/> is the projection built for this.</para>
/// </remarks>
[ApiController]
[Route("api/employee-portal/profile")]
[Authorize(Policy = "InternalOnly")]
public class MyProfileController : ControllerBase
{
    private readonly IEmployeeProfileChangeService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<MyProfileController> _logger;

    public MyProfileController(
        IEmployeeProfileChangeService service,
        IHrControlledDocumentService hrDocuments,
        ICurrentUserService currentUser,
        ILogger<MyProfileController> logger)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _currentUser = currentUser;
        _logger = logger;
    }

    private IActionResult NoEmployee() =>
        Problem(
            detail:     "Your account is not linked to an employee record. Please contact HR.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    // ── The profile ───────────────────────────────────────────────────────────

    /// <summary>The caller's own profile.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _service.GetMyProfileAsync(empId, ct));
    }

    /// <summary>
    /// Updates the contact fields the employee owns outright. Everything else needs a change
    /// request — see <see cref="CreateChangeRequest"/>.
    /// </summary>
    [HttpPut("contact-details")]
    public async Task<IActionResult> UpdateContactDetails(
        [FromBody] UpdateMyContactDetailsDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _service.UpdateMyContactDetailsAsync(empId, dto, ct));
    }

    // ── Change requests ───────────────────────────────────────────────────────

    /// <summary>Files a request to change identity-, address-, statutory- or bank-bearing data.</summary>
    [HttpPost("change-requests")]
    public async Task<IActionResult> CreateChangeRequest(
        [FromBody] CreateProfileChangeRequestDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        try
        {
            var created = await _service.CreateRequestAsync(empId, dto, ct);
            return CreatedAtAction(nameof(GetChangeRequest), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    /// <summary>The caller's own requests, newest first.</summary>
    [HttpGet("change-requests")]
    public async Task<IActionResult> GetMyChangeRequests(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _service.GetMineAsync(empId, ct));
    }

    /// <summary>One of the caller's own requests. Somebody else's id is a 404, never a 403.</summary>
    [HttpGet("change-requests/{id:guid}")]
    public async Task<IActionResult> GetChangeRequest(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var request = await _service.GetByIdAsync(id, empId, isHrDesk: false, ct);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>Withdraws a pending request of the caller's own.</summary>
    [HttpPost("change-requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelChangeRequest(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        try
        {
            return Ok(await _service.CancelAsync(id, empId, ct));
        }
        // A request that is not the caller's does not resolve at all — the service scopes the
        // lookup, so there is no UnauthorizedAccessException arm here by design.
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Attaches the evidence behind a pending request — a marriage certificate, a bank letter.
    /// Rides the shared controlled-upload gate (scan, checksum, DMS registration) like every
    /// other HR attachment.
    /// </summary>
    [HttpPost("change-requests/{id:guid}/evidence")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadEvidence(
        Guid id, IFormFile file, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        // Entitlement BEFORE storage: refuse a file for a request that is not the caller's, or
        // is already answered, rather than scanning and storing it first.
        var request = await _service.GetByIdAsync(id, empId, isHrDesk: false, ct);
        if (request is null) return NotFound();

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "EmployeeProfileChangeRequest",
            sourceRecordId: id,
            sourceLabel: request.RequestNumber,
            documentType: "Personal data change evidence",
            description: request.Reason,
            persist: async (_, document) =>
            {
                await _service.AttachEvidenceAsync(
                    id, empId,
                    document.FileUploadRecordId,
                    document.DocumentRecordId,
                    document.DocumentVersionId,
                    document.FilePath,
                    document.OriginalFileName,
                    document.ContentType,
                    document.FileSize,
                    ct);
                return await _service.GetByIdAsync(id, empId, isHrDesk: false, ct);
            },
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrProfileChangeEvidence);
    }

    /// <summary>Reads back the evidence the caller attached to their own request.</summary>
    [HttpGet("change-requests/{id:guid}/evidence")]
    public async Task<IActionResult> DownloadOwnEvidence(
        [FromServices] ErpSystem.Data.ApplicationDbContext db,
        [FromServices] ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService centralDocuments,
        [FromServices] IFileStorageService fileStorage,
        Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        if (_currentUser.TenantId is not Guid tenantId) return NoEmployee();

        // Ownership first: a request that is not the caller's does not resolve at all.
        var request = await _service.GetByIdAsync(id, empId, isHrDesk: false, ct);
        if (request is null || !request.HasEvidence) return NotFound();

        var row = await db.EmployeeProfileChangeRequests.FindAsync([id], ct);
        if (row is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, centralDocuments, fileStorage, db, tenantId,
            row.EvidenceDocumentRecordId, row.EvidenceDocumentVersionId,
            row.EvidenceFileUploadRecordId, row.EvidenceFilePath,
            row.EvidenceFileName ?? "evidence", row.EvidenceContentType,
            inline: false, cancellationToken: ct);
    }
}
