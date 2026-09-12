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
/// Staff announcements — the desk that writes and publishes them (area 25 slice 12c, D7).
/// </summary>
/// <remarks>
/// <para>Gated on the <c>HR.Company</c> family rather than <c>HR.Employee</c>: an announcement is
/// a communication from the organisation, not an operation on anybody's record. Reading the
/// register is <c>CompanyRead</c>; writing and publishing are <c>CompanyWrite</c>.</para>
///
/// <para>The lifecycle is Draft → Published → Archived, and it only goes forward. A published
/// notice is never edited in place — people have read it, and silently changing what they were
/// told is worse than publishing a correction.</para>
/// </remarks>
[ApiController]
[Route("api/hr/announcements")]
[Authorize(Policy = "InternalOnly")]
public class HrAnnouncementsController : ControllerBase
{
    private readonly IHrAnnouncementService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<HrAnnouncementsController> _logger;

    public HrAnnouncementsController(
        IHrAnnouncementService service,
        IHrControlledDocumentService hrDocuments,
        ICurrentUserService currentUser,
        ILogger<HrAnnouncementsController> logger)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _currentUser = currentUser;
        _logger = logger;
    }

    private IActionResult NoActor() =>
        Problem(
            detail:     "Your account is not linked to an employee record, so a publication cannot be attributed to you.",
            statusCode: StatusCodes.Status403Forbidden,
            title:      "Employee Account Not Linked");

    [HttpGet]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> GetAll(
        [FromQuery] HrAnnouncementStatus? status, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(status, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var announcement = await _service.GetByIdAsync(id, ct);
        return announcement is null ? NotFound() : Ok(announcement);
    }

    /// <summary>
    /// How many employees a set of audience rules reaches — for the sender to see BEFORE they
    /// publish. Takes the rules in the body rather than an id, so the count updates as the form
    /// is edited and not only once the draft has been saved.
    /// </summary>
    [HttpPost("audience-preview")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> PreviewAudience(
        [FromBody] List<HrAnnouncementAudienceDto> audiences, CancellationToken ct = default)
        => Ok(new { count = await _service.PreviewAudienceCountAsync(audiences ?? [], ct) });

    [HttpPost]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Create(
        [FromBody] CreateHrAnnouncementDto dto, CancellationToken ct = default)
    {
        try
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateHrAnnouncementDto dto, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Publishes it. Refused when the audience reaches nobody.</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid actorId) return NoActor();

        try
        {
            return Ok(await _service.PublishAsync(id, actorId, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.EmployeeId is not Guid actorId) return NoActor();

        try
        {
            return Ok(await _service.ArchiveAsync(id, actorId, ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Deletes a DRAFT. A published announcement is archived, never deleted.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _service.DeleteDraftAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    /// <summary>Attaches a file — the notice everyone is being pointed at.</summary>
    [HttpPost("{id:guid}/attachment")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadAttachment(
        Guid id, IFormFile file, CancellationToken ct = default)
    {
        // Entitlement before storage: no scanning and storing a file for a record that is not there.
        var announcement = await _service.GetByIdAsync(id, ct);
        if (announcement is null) return NotFound();

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "HrAnnouncement",
            sourceRecordId: id,
            sourceLabel: announcement.Title,
            documentType: "Staff announcement attachment",
            description: announcement.Summary,
            persist: async (_, document) =>
            {
                await _service.AttachFileAsync(
                    id,
                    document.FileUploadRecordId,
                    document.DocumentRecordId,
                    document.DocumentVersionId,
                    document.FilePath,
                    document.OriginalFileName,
                    document.ContentType,
                    document.FileSize,
                    ct);
                return await _service.GetByIdAsync(id, ct);
            },
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrAnnouncementDocuments);
    }
}
