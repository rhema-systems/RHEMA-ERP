using ErpSystem.Api.Filters;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
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

/// <summary>
/// Job postings — a vacancy's advert on one channel.
///
/// <para>Reads are open to the tenant (an internal advert is meant to be seen); creating,
/// editing, publishing and expiring one is HR's. Publication additionally refuses unless the
/// vacancy behind the advert is itself Published — see <c>JobPostingService.PublishAsync</c>.</para>
/// </summary>
[ApiController]
[Route("api/job-postings")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class JobPostingController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IJobPostingService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<JobPostingController> _logger;

    public JobPostingController(
        IJobPostingService service,
        ICurrentUserService currentUser,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ILogger<JobPostingController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorageService = fileStorageService;
        _db = db;
        _logger = logger;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobPostingDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("vacancy/{vacancyId:guid}")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetByVacancy(Guid vacancyId)
        => Ok(await _service.GetByVacancyIdAsync(vacancyId));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetActive()
        => Ok(await _service.GetActivePostingsAsync());

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetByStatus(JobPostingStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("channel/{channel}")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetByChannel(JobPostingChannel channel)
        => Ok(await _service.GetByChannelAsync(channel));

    [HttpGet("expired-active")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetExpiredActive()
        => Ok(await _service.GetExpiredActivePostingsAsync());

    [HttpGet("external/{externalPostingId}")]
    public async Task<ActionResult<JobPostingDto?>> GetByExternalId(string externalPostingId)
        => Ok(await _service.GetByExternalPostingIdAsync(externalPostingId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobPostingDto>> Create([FromBody] CreateJobPostingDto dto)
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

    [HttpPut("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobPostingDto>> Update(Guid id, [FromBody] UpdateJobPostingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/expire")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> Expire(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ExpireAsync(id, employeeId.Value);
        return Ok(new { message = "Posting expired." });
    }

    /// <summary>Records that the posting was actually published (optionally on a specific date).</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<JobPostingDto>> Publish(Guid id, [FromBody] PublishJobPostingDto? dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        try
        {
            return Ok(await _service.PublishAsync(id, dto?.ActualPublishDate, employeeId.Value));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<JobPostingAttachmentDto>>> GetAttachments(Guid id)
        => Ok(await _service.GetAttachmentsAsync(id));

    /// <summary>
    /// Attaches a document to a posting through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// Replaced a JSON endpoint that accepted a caller-supplied <c>filePath</c>: it stored no file,
    /// scanned nothing, and recorded whatever path was posted.
    ///
    /// <para>⚠ Requires a working ClamAV — <c>hr-recruitment-attachments</c> is scan-mandatory.</para>
    /// </remarks>
    [HttpPost("{id:guid}/attachments")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(JobPostingAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        Guid id, IFormFile file, [FromForm] string? description, CancellationToken ct)
    {
        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "JobPosting",
            sourceRecordId: id,
            sourceLabel: "Job posting attachment",
            documentType: "JobPostingAttachment",
            description: description,
            persist: (uploadedById, document) => _service.AddAttachmentAsync(
                id, uploadedById, document.OriginalFileName, document.FileSize, description, ct,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken: ct,
            category: ControlledFileUploadCategories.HrRecruitmentAttachments);
    }

    /// <summary>Streams a posting attachment — the file lives outside the web root.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<JobPostingAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.JobPostingId == id && a.TenantId == tenantId && !a.IsDeleted,
                ct);

        if (attachment is null)
            return NotFound(new { message = "Attachment not found" });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorageService, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FilePath,
            attachment.FileName, fallbackContentType: null,
            inline: false, cancellationToken: ct);
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        var ok = await _service.DeleteAttachmentAsync(attachmentId);
        return ok ? NoContent() : NotFound();
    }
}
