using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The HR desk queue for employee letter requests (area 25 slice 12b, decision D7).
/// </summary>
/// <remarks>
/// <para>Two ways to fulfil one: <b>generate</b> the letter from the HR-editable template — the
/// normal path, since every fact in an employment confirmation is already in the system — or
/// <b>upload</b> a signed scan for the letters that need a wet signature or somebody else's
/// form. <see cref="Preview"/> renders it without issuing, because a document nobody read
/// before issuing is how a wrong salary reaches a bank.</para>
///
/// <para>Gated on <c>EmployeeRead</c>/<c>EmployeeWrite</c>: issuing a letter is making a
/// statement about an employee record, which is the permission family that already governs it.
/// No new permission family (D8).</para>
/// </remarks>
[ApiController]
[Route("api/hr/letter-requests")]
[Authorize(Policy = "InternalOnly")]
public class HrLetterRequestsController : ControllerBase
{
    private readonly IHrLetterRequestService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<HrLetterRequestsController> _logger;

    public HrLetterRequestsController(
        IHrLetterRequestService service,
        IHrControlledDocumentService hrDocuments,
        ICurrentUserService currentUser,
        ILogger<HrLetterRequestsController> logger)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _currentUser = currentUser;
        _logger = logger;
    }

    private IActionResult NoIssuer() =>
        Problem(
            detail:     "Your account is not linked to an employee record, so a letter cannot be attributed to you.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    /// <summary>The queue — pending first, then what has been answered.</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] HrLetterRequestStatus? status, CancellationToken ct = default)
        => Ok(await _service.GetQueueAsync(status, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var actor = _currentUser.EmployeeId ?? Guid.Empty;
        var request = await _service.GetByIdAsync(id, actor, isHrDesk: true, ct);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>Renders the letter WITHOUT issuing it, so HR can read it first.</summary>
    [HttpGet("{id:guid}/preview")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _service.PreviewAsync(id, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Issues the generated letter and freezes it on the request.</summary>
    [HttpPost("{id:guid}/issue")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<IActionResult> Issue(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid issuerId) return NoIssuer();

        try
        {
            return Ok(await _service.IssueGeneratedAsync(id, issuerId, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>The issued letter as HR sees it — the same frozen copy the employee reads.</summary>
    [HttpGet("{id:guid}/document")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken ct = default)
    {
        var actor = _currentUser.EmployeeId ?? Guid.Empty;
        var document = await _service.GetIssuedDocumentAsync(id, actor, isHrDesk: true, ct);
        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>
    /// Fulfils the request with a SIGNED SCAN instead of a generated letter — for the letters
    /// that need a wet signature, a stamp, or somebody else's form.
    /// </summary>
    [HttpPost("{id:guid}/upload")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadSignedLetter(
        Guid id, IFormFile file, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid issuerId) return NoIssuer();

        // Entitlement before storage: refuse for a request that does not exist or is already
        // answered, rather than scanning and storing the file first.
        var request = await _service.GetByIdAsync(id, issuerId, isHrDesk: true, ct);
        if (request is null) return NotFound();
        if (request.Status != HrLetterRequestStatus.Pending)
            return UnprocessableEntity(new
            {
                message = $"This request has already been {request.StatusName.ToLowerInvariant()}.",
            });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "HrLetterRequest",
            sourceRecordId: id,
            sourceLabel: request.RequestNumber,
            documentType: $"HR letter — {request.LetterTypeName}",
            description: request.Purpose,
            persist: async (_, document) =>
            {
                await _service.AttachIssuedFileAsync(
                    id, issuerId,
                    document.FileUploadRecordId,
                    document.DocumentRecordId,
                    document.DocumentVersionId,
                    document.FilePath,
                    document.OriginalFileName,
                    document.ContentType,
                    document.FileSize,
                    ct);
                return await _service.GetByIdAsync(id, issuerId, isHrDesk: true, ct);
            },
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrLetterDocuments);
    }

    /// <summary>Refuses the request. The comment is required — the employee reads it back.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<IActionResult> Reject(
        Guid id, [FromBody] RejectHrLetterRequestDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid issuerId) return NoIssuer();

        try
        {
            return Ok(await _service.RejectAsync(id, issuerId, dto ?? new(), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }
}
