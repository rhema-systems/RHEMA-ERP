using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Staff announcements, as the employee sees them (area 25 slice 12c, decision D7).
/// </summary>
/// <remarks>
/// Read-only by definition. Audience membership is evaluated per request, so an announcement
/// aimed at a unit the caller is not in simply does not appear — and asking for it by id is a
/// lookup miss, not a refusal, because a 403 would tell them an announcement exists that they
/// were not sent.
/// </remarks>
[ApiController]
[Route("api/employee-portal/announcements")]
[Authorize(Policy = "InternalOnly")]
public class MyAnnouncementsController : ControllerBase
{
    private readonly IHrAnnouncementService _service;
    private readonly ICurrentUserService _currentUser;

    public MyAnnouncementsController(IHrAnnouncementService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private IActionResult NoEmployee() =>
        Problem(
            detail:     "Your account is not linked to an employee record. Please contact HR.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    /// <summary>Everything live and aimed at the caller: pinned first, then newest.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        return Ok(await _service.GetMineAsync(empId, ct));
    }

    /// <summary>One announcement, if it is live and aimed at the caller.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();

        var announcement = await _service.GetMineByIdAsync(id, empId, ct);
        return announcement is null ? NotFound() : Ok(announcement);
    }

    /// <summary>Downloads an announcement's attachment.</summary>
    [HttpGet("{id:guid}/attachment")]
    public async Task<IActionResult> DownloadAttachment(
        [FromServices] ErpSystem.Data.ApplicationDbContext db,
        [FromServices] ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService centralDocuments,
        [FromServices] IFileStorageService fileStorage,
        Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid empId) return NoEmployee();
        if (_currentUser.TenantId is not Guid tenantId) return NoEmployee();

        // Entitlement first, through the same audience check as the read: an attachment must
        // not be reachable by anyone the announcement itself was not sent to.
        var announcement = await _service.GetMineByIdAsync(id, empId, ct);
        if (announcement is null || !announcement.HasAttachment) return NotFound();

        var row = await db.HrAnnouncements.FindAsync([id], ct);
        if (row is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, centralDocuments, fileStorage, db, tenantId,
            row.DocumentRecordId, row.DocumentVersionId,
            row.FileUploadRecordId, row.FilePath,
            row.FileName ?? "attachment", row.ContentType,
            inline: false, cancellationToken: ct);
    }
}
