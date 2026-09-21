using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
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
/// Files that belong to a team's charter or its tasks, through the controlled gate.
/// </summary>
/// <remarks>
/// <para>Round 2, lane F1. Its own controller for the same reason
/// <c>EmployeeDocumentsController</c> is: a gated upload needs a multipart action and a
/// token-bearing download, and those two shapes do not belong on a JSON controller.</para>
///
/// <para><b>⚠ There is no JSON create for an attachment.</b> A row exists only as the result of an
/// upload here, which virus-scans the file and registers it in the central DMS before HR sees an
/// id. A caller may say what a file IS and may never say where it lives — the sink that D-10, D-14
/// and D-39 each had to remove after they had already shipped.</para>
///
/// <para><b>⚠ Entitlement is checked before storage is touched</b>, not after. Neither the gate nor
/// the DMS asks whether this caller may write to this team, so an upload that scanned and stored
/// first would leave a file in the repository for a team the caller has nothing to do with, even
/// though the row that points at it is refused.</para>
/// </remarks>
[ApiController]
[Route("api/hr/team-documents")]
[Authorize(Policy = "InternalOnly")]
[TeamActivityBusinessRules]
public class TeamActivityDocumentsController : ControllerBase
{
    private readonly ITeamActivityService _service;
    private readonly ITeamMeetingService _meetings;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TeamActivityDocumentsController> _logger;

    public TeamActivityDocumentsController(
        ITeamActivityService service,
        ITeamMeetingService meetings,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        ApplicationDbContext db,
        ILogger<TeamActivityDocumentsController> logger)
    {
        _service = service;
        _meetings = meetings;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _db = db;
        _logger = logger;
    }

    /// <summary>The signed charter for a version of a team's terms of reference.</summary>
    [HttpPost("terms/{termsId:guid}/document")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadTermsDocument(
        Guid termsId, IFormFile? file, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid) return BadRequest("Tenant context could not be resolved.");

        // ⚠ Entitlement first, storage second. `GetTermsByIdAsync` throws 403 through the filter for
        // a caller who is nothing to do with the team, and 404 for a row that is not there.
        var terms = await _service.GetTermsByIdAsync(termsId, ct);
        if (terms is null) return NotFound(new { message = $"Terms of reference '{termsId}' were not found." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: nameof(Core.Entities.HR.TeamTermsOfReference),
            sourceRecordId: termsId,
            sourceLabel: "Team terms of reference",
            documentType: "TeamTermsOfReference",
            description: null,
            persist: async (_, document) =>
            {
                await _service.AttachTermsDocumentAsync(
                    termsId, document.FileUploadRecordId, document.DocumentRecordId,
                    document.DocumentVersionId, document.OriginalFileName, document.ContentType,
                    document.FileSize, ct);
                return true;
            },
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrEmployeeDocuments);
    }

    [HttpGet("terms/{termsId:guid}/document")]
    public async Task<IActionResult> DownloadTermsDocument(Guid termsId, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var reference = await _service.GetTermsDocumentRefAsync(termsId, ct);
        if (reference is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            reference.DocumentRecordId, reference.DocumentVersionId, reference.FileUploadRecordId,
            legacyPath: null,
            reference.FileName ?? "terms-of-reference",
            fallbackContentType: reference.MimeType,
            inline: false, ct);
    }

    /// <summary>A file pertaining to a task — a draft, a quote, a photograph of the thing done.</summary>
    [HttpPost("tasks/{taskId:guid}/attachments")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadTaskAttachment(
        Guid taskId,
        IFormFile? file,
        [FromForm] string? title = null,
        CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid) return BadRequest("Tenant context could not be resolved.");

        var task = await _service.GetTaskByIdAsync(taskId, ct);
        if (task is null) return NotFound(new { message = $"Task '{taskId}' was not found." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: nameof(Core.Entities.HR.TeamTask),
            sourceRecordId: taskId,
            sourceLabel: "Team task attachment",
            documentType: "TeamTaskAttachment",
            description: null,
            persist: (_, document) => _service.AddTaskAttachmentAsync(
                taskId, title, document.FileUploadRecordId, document.DocumentRecordId,
                document.DocumentVersionId, document.OriginalFileName, document.ContentType,
                document.FileSize, ct),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrEmployeeDocuments);
    }

    /// <summary>The signed minutes of a meeting (round 2, lane F2).</summary>
    [HttpPost("meetings/{meetingId:guid}/minutes")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadMeetingMinutes(
        Guid meetingId, IFormFile? file, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid) return BadRequest("Tenant context could not be resolved.");

        // ⚠ Entitlement first, storage second — the read throws 403 through the filter for a caller
        // who is nothing to do with the team, and 404 for a meeting that is not there.
        var meeting = await _meetings.GetMeetingAsync(meetingId, ct);
        if (meeting is null) return NotFound(new { message = $"Meeting '{meetingId}' was not found." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: nameof(Core.Entities.HR.TeamMeeting),
            sourceRecordId: meetingId,
            sourceLabel: "Team meeting minutes",
            documentType: "TeamMeetingMinutes",
            description: null,
            persist: async (_, document) =>
            {
                await _meetings.AttachMinutesDocumentAsync(
                    meetingId, document.FileUploadRecordId, document.DocumentRecordId,
                    document.DocumentVersionId, document.OriginalFileName, document.ContentType,
                    document.FileSize, ct);
                return true;
            },
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrEmployeeDocuments);
    }

    [HttpGet("meetings/{meetingId:guid}/minutes")]
    public async Task<IActionResult> DownloadMeetingMinutes(Guid meetingId, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var reference = await _meetings.GetMinutesDocumentRefAsync(meetingId, ct);
        if (reference is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            reference.DocumentRecordId, reference.DocumentVersionId, reference.FileUploadRecordId,
            legacyPath: null,
            reference.FileName ?? "minutes",
            fallbackContentType: reference.MimeType,
            inline: false, ct);
    }

    [HttpGet("tasks/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadTaskAttachment(Guid attachmentId, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var reference = await _service.GetTaskAttachmentRefAsync(attachmentId, ct);
        if (reference is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            reference.DocumentRecordId, reference.DocumentVersionId, reference.FileUploadRecordId,
            legacyPath: null,
            reference.FileName ?? "attachment",
            fallbackContentType: reference.MimeType,
            inline: false, ct);
    }
}
