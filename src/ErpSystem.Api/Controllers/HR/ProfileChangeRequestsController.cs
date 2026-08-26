using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The HR desk queue for personal-data change requests (area 25 slice 12, decision D6).
/// </summary>
/// <remarks>
/// <para>The employee's half lives on <c>api/employee-portal/profile</c>. This is the other
/// side: read the queue, and answer. <b>Approving APPLIES</b> the values in the same
/// transaction and re-runs the same tenant-uniqueness checks the desk employee-update path
/// enforces, so this cannot become a way around them.</para>
///
/// <para>Gated on <c>EmployeeWritePolicy</c> for the answer and <c>EmployeeReadPolicy</c> for
/// the queue — the same permissions that already govern changing an employee record, because
/// that is exactly what an approval does. No new permission family (D8).</para>
/// </remarks>
[ApiController]
[Route("api/hr/profile-change-requests")]
[Authorize(Policy = "InternalOnly")]
public class ProfileChangeRequestsController : ControllerBase
{
    private readonly IEmployeeProfileChangeService _service;
    private readonly ICurrentUserService _currentUser;

    public ProfileChangeRequestsController(
        IEmployeeProfileChangeService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private IActionResult NoReviewer() =>
        Problem(
            detail:     "Your account is not linked to an employee record, so an approval cannot be attributed to you.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    /// <summary>The queue — pending first, then the answered history.</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] ProfileChangeRequestStatus? status, CancellationToken ct = default)
        => Ok(await _service.GetQueueAsync(status, ct));

    /// <summary>One request, with its before/after values and the employee's stated reason.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var actor = _currentUser.EmployeeId ?? Guid.Empty;
        var request = await _service.GetByIdAsync(id, actor, isHrDesk: true, ct);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>Approves it AND writes the values onto the employee record.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<IActionResult> Approve(
        Guid id, [FromBody] ReviewProfileChangeRequestDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid reviewerId) return NoReviewer();

        try
        {
            return Ok(await _service.ApproveAsync(id, reviewerId, dto ?? new(), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Refuses it. The comment is required — the employee reads it back.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    public async Task<IActionResult> Reject(
        Guid id, [FromBody] ReviewProfileChangeRequestDto dto, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid reviewerId) return NoReviewer();

        try
        {
            return Ok(await _service.RejectAsync(id, reviewerId, dto ?? new(), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Downloads the evidence the employee attached.</summary>
    [HttpGet("{id:guid}/evidence")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    public async Task<IActionResult> DownloadEvidence(
        [FromServices] ErpSystem.Data.ApplicationDbContext db,
        [FromServices] ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService centralDocuments,
        [FromServices] IFileStorageService fileStorage,
        Guid id, CancellationToken ct = default)
    {
        var actor = _currentUser.EmployeeId ?? Guid.Empty;
        var request = await _service.GetByIdAsync(id, actor, isHrDesk: true, ct);
        if (request is null || !request.HasEvidence) return NotFound();

        if (_currentUser.TenantId is not Guid tenantId) return NoReviewer();

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
