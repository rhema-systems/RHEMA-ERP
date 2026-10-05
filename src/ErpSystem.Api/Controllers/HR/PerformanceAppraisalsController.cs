using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
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

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class PerformanceAppraisalsController : ControllerBase
{
    private readonly IPerformanceAppraisalService _appraisalService;
    private readonly IPeerNominationService _peerNominationService;
    private readonly IAppraisalWithdrawalService _withdrawals;
    private readonly IEffectiveAppraisalConfigurationService _configuration;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PerformanceAppraisalsController> _logger;

    public PerformanceAppraisalsController(
        IPerformanceAppraisalService appraisalService,
        IPeerNominationService peerNominationService,
        IAppraisalWithdrawalService withdrawals,
        IEffectiveAppraisalConfigurationService configuration,
        ICurrentUserService currentUserService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ILogger<PerformanceAppraisalsController> logger)
    {
        _appraisalService = appraisalService;
        _peerNominationService = peerNominationService;
        _withdrawals = withdrawals;
        _configuration = configuration;
        _currentUserService = currentUserService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorageService = fileStorageService;
        _db = db;
        _logger = logger;
    }

    // ── Actor and error helpers ─────────────────────────────────────────────────────
    //
    // Every action in the appraisal run is taken *as* an employee — the appraisee, their
    // manager, a nominated peer, the HR reviewer. The id therefore comes from the token, never
    // from the request body: the services validate the id they are given against the appraisal,
    // which stops a caller acting on the wrong appraisal but not on the wrong person's behalf.

    private bool TryGetEmployeeId(out Guid employeeId, out IActionResult? problem)
    {
        var id = _currentUserService.EmployeeId;
        if (id is null || id == Guid.Empty)
        {
            employeeId = Guid.Empty;
            problem = BadRequest(new { message = "Your account is not linked to an employee record, so it cannot take part in an appraisal." });
            return false;
        }

        employeeId = id.Value;
        problem = null;
        return true;
    }

    /// <summary>
    /// Business rules raised by the services are answered with 422 and the rule's own message,
    /// matching <c>EmployeeGoalsController</c>, so a client can read <c>.message</c> instead of
    /// getting a body it cannot parse. These are expected outcomes, so they log at warning.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Appraisal rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// Get all performance appraisals
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _appraisalService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _appraisalService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisal by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        // The appraisee, their manager, or the desk — a raw id must not read anyone's appraisal.
        if (!await CanAccessAppraisalAsync(id, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            var response = await _appraisalService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisal with {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the performance appraisal");
        }
    }

    /// <summary>
    /// Get performance appraisals by employee ID
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId)
    {
        if (!await CanAccessEmployeeRecordsAsync(employeeId, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            var response = await _appraisalService.GetByEmployeeIdAsync(employeeId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals by year
    /// </summary>
    [HttpGet("year/{year}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetByYear(int year)
    {
        try
        {
            var response = await _appraisalService.GetByYearAsync(year);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    /// <summary>
    /// Get performance appraisals by status
    /// </summary>
    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceAppraisalDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetByStatus(AppraisalStatus status)
    {
        try
        {
            var response = await _appraisalService.GetByStatusAsync(status);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance appraisals");
            return StatusCode(500, "An error occurred while retrieving performance appraisals");
        }
    }

    // POST api/PerformanceAppraisals (the raw create) went in performance closure E-d2b (D-20, D-44): no screen called
    // it, it took no template, snapshot or evaluations, and it checked neither the cycle nor the employee. An appraisal
    // is generated, on an Open cycle: POST api/AppraisalCycle/{id}/generate-appraisals.

    /// <summary>
    /// Update an existing performance appraisal
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePerformanceAppraisalDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "correcting the appraisal's dates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while updating the performance appraisal");
        }
    }

    /// <summary>
    /// Update appraisal status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAppraisalStatusDto statusDto)
    {
        try
        {
            if (id != statusDto.AppraisalId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _appraisalService.UpdateStatusAsync(statusDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A move the raw route does not make names the action that makes it (E-a).
            return BusinessRuleRejected(ex, "changing the appraisal's status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while updating the performance appraisal");
        }
    }

    /// <summary>
    /// What the settle path would store for this appraisal now — the computed overall, a panel's
    /// restatement, the settled overall, grade and rating — beside what is stored, writing nothing
    /// (performance closure E-a). It replaced <c>POST {id}/calculate-score</c>, which stored the score
    /// with no status check and published a final appraisal's rating to the talent pools, so it
    /// restated finalised scores (against D-13). The desk's read, and not an appraisee's own.
    /// </summary>
    [HttpGet("{id:guid}/score-preview")]
    [ProducesResponseType(typeof(AppraisalSettleDryRunRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> ScorePreview(
        Guid id, [FromServices] IAppraisalScoreService scores, CancellationToken cancellationToken)
    {
        // The two-actor rule: an HR officer's own appraisal is released to them as to any appraisee.
        if (await IsOwnAppraisalAsync(id, cancellationToken))
            return StatusCode(403, new { message = "You cannot preview your own appraisal's score: another HR officer does." });

        try
        {
            return Ok(await scores.PreviewAsync(id, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing the score of appraisal {AppraisalId}", id);
            return StatusCode(500, "An error occurred while previewing the appraisal's score");
        }
    }

    /// <summary>
    /// Performance closure A15 (D-13): what the settle path would store for every Completed or
    /// Closed appraisal — overall, grade and rating — beside what is stored now, with the
    /// employee's current talent-pool rating. Read-only: nothing is written, and a finalised score
    /// is restated only through the audited reopen.
    /// </summary>
    [HttpGet("settle-dry-run")]
    [ProducesResponseType(typeof(AppraisalSettleDryRunReportDto), StatusCodes.Status200OK)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> SettleDryRun(
        [FromQuery] Guid? cycleId, [FromServices] IAppraisalScoreService scores, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await scores.DryRunAsync(cycleId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running the settle dry run (cycle {CycleId})", cycleId);
            return StatusCode(500, "An error occurred while running the settle dry run");
        }
    }

    // ⚠ POST {id}/appeal and POST appeal/{appealId}/resolve — the legacy appeal pair — were removed in
    // performance closure C1: the one filed an appeal past every rule, the other set any status and
    // AdjustedScore on any appeal. No screen called either. submit-appeal and resolve-appeal below.

    /// <summary>
    /// Delete a performance appraisal
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _appraisalService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "removing the appraisal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting performance appraisal with Id {AppraisalId}", id);
            return StatusCode(500, "An error occurred while deleting the performance appraisal");
        }
    }

    // ⚠ Performance closure lane P1 (2026-09-29) removed eight routes here: add/read/update/delete
    // an evaluator evaluation, and add/read/update/delete a criterion score. No screen called them.
    // GET {appraisalId}/evaluations admitted the appraisee (CanAccessAppraisalAsync), so it handed
    // them every evaluator row — peer names and scores under anonymity, the manager's total, notes
    // and recommendation before sign-off; the writes could re-parent a record or score it outside
    // the settle path. HR reads evaluator totals through GET {id}/hr-review and
    // GET {appraisalId}/manager-peer-evaluations.

    #region Employee Response Operations

    /// <summary>
    /// Add an employee response to an appraisal
    /// </summary>
    [HttpPost("{appraisalId}/responses")]
    [ProducesResponseType(typeof(AppraisalEmployeeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    /// <remarks>
    /// ⚠ <b>Deliberately not wired to any screen.</b> This is the HR desk transcribing a response
    /// the employee gave on paper. The employee's own answer goes through
    /// <c>POST api/performance-appraisals/me/{appraisalId}/responses</c>, which refuses anyone but
    /// the appraisal's own employee — and that route is the only thing that makes the record mean
    /// what it says, because AppraisalEmployeeResponse carries no author column to check.
    /// Same disposition, and the same reasoning, as the travel desk's alert acknowledgement (D-32).
    /// </remarks>
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> AddEmployeeResponse(Guid appraisalId, [FromBody] CreateAppraisalEmployeeResponseDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            Guid.TryParse(_currentUserService.UserId, out var actingUserId);
            var response = await _appraisalService.AddEmployeeResponseAsync(
                appraisalId, createDto, actingUserId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // The service's rules — responses switched off for the cycle, a withdrawn appraisal
            // (performance closure E-d1) — answered 500 through the catch-all below.
            return BusinessRuleRejected(ex, "adding an employee response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding employee response to appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while adding the employee response");
        }
    }

    /// <summary>
    /// Get all employee responses for an appraisal
    /// </summary>
    [HttpGet("{appraisalId}/responses")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalEmployeeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeeResponses(Guid appraisalId)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            var response = await _appraisalService.GetEmployeeResponsesAsync(appraisalId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee responses for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving employee responses");
        }
    }

    #endregion

    #region Attachment Operations

    /// <summary>W3: whether the caller holds the given performance policy (seed and role fallback both count).</summary>
    private async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>
    /// True when the caller may touch this appraisal: the appraisee, their line manager, or a
    /// holder of <paramref name="policy"/> (Read for reads, Write for writes — the W3 tiering of
    /// the old HR-role arm). Mirrors the rule on <see cref="AppraisalReviewEventsController"/>.
    /// </summary>
    private async Task<bool> CanAccessAppraisalAsync(Guid appraisalId, string policy, CancellationToken ct)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<PerformanceAppraisal>()
            .AsNoTracking()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .AnyAsync(a => a.EmployeeId == me || a.Employee.ManagerId == me, ct);
    }

    /// <summary>
    /// The caller is this appraisal's appraisee — the two-actor rule on a desk read that is not the
    /// appraisee's (performance closure E-a). The desk's writes apply it in the service.
    /// </summary>
    private async Task<bool> IsOwnAppraisalAsync(Guid appraisalId, CancellationToken ct)
    {
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<PerformanceAppraisal>()
            .AsNoTracking()
            .AnyAsync(a => a.Id == appraisalId && a.TenantId == tenantId && a.EmployeeId == me, ct);
    }

    /// <summary>
    /// As <see cref="CanAccessAppraisalAsync"/> minus the appraisee — the manager's side of the
    /// run (peer detail under anonymity, nomination decisions) is not the appraisee's to reach.
    /// </summary>
    /// <remarks>
    /// Not an HR officer who is the appraisee either (performance closure P14, the two-actor rule):
    /// the policy test came first, so the desk read its own peers' names under anonymity and
    /// approved its own nominations.
    /// </remarks>
    private async Task<bool> CanManageAppraisalAsync(Guid appraisalId, string policy, CancellationToken ct)
    {
        if (_currentUserService.TenantId is not Guid tenantId) return false;
        var me = _currentUserService.EmployeeId is Guid id && id != Guid.Empty ? id : (Guid?)null;

        var parties = await _db.Set<PerformanceAppraisal>()
            .AsNoTracking()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .Select(a => new { a.EmployeeId, a.Employee.ManagerId })
            .FirstOrDefaultAsync(ct);

        // An unknown id falls to the desk, so the action reports it missing rather than forbidden.
        if (parties is null) return await HoldsPolicyAsync(policy);
        if (me is Guid subject && parties.EmployeeId == subject) return false;
        if (me is Guid manager && parties.ManagerId == manager) return true;
        return await HoldsPolicyAsync(policy);
    }

    /// <summary>The employee themselves, their line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessEmployeeRecordsAsync(Guid employeeId, string policy, CancellationToken ct)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
        {
            if (me == employeeId) return true;
            if (_currentUserService.TenantId is Guid tenantId &&
                await _db.Set<Core.Entities.HR.Employee>()
                    .AsNoTracking()
                    .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == me, ct))
                return true;
        }

        return await HoldsPolicyAsync(policy);
    }

    /// <summary>The named actor from the route is the caller, or the caller holds the policy.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId) return true;
        return await HoldsPolicyAsync(policy);
    }

    /// <summary>
    /// Attach a file to an appraisal, through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// Replaces a JSON endpoint that took a caller-supplied <c>filePath</c> and could not have
    /// worked in any case — see <c>PerformanceAppraisalService.AddAttachmentAsync</c>. The
    /// entitlement test is new too: appraisal evidence had no gate beyond <c>[Authorize]</c>.
    /// </remarks>
    [HttpPost("{appraisalId:guid}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        Guid appraisalId, IFormFile file, [FromForm] string? description, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUserService, _logger, file,
            sourceEntityType: "PerformanceAppraisal",
            sourceRecordId: appraisalId,
            sourceLabel: "Appraisal attachment",
            documentType: "AppraisalAttachment",
            description: description,
            persist: (uploadedById, document) => _appraisalService.AddAttachmentAsync(
                appraisalId, uploadedById, document.OriginalFileName, document.FileSize, description,
                cancellationToken,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken);
    }

    /// <summary>Streams an appraisal attachment — the file lives outside the web root.</summary>
    [HttpGet("{appraisalId:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid appraisalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<AppraisalAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.PerformanceAppraisalId == appraisalId && a.TenantId == tenantId && !a.IsDeleted,
                cancellationToken);

        if (attachment is null)
            return NotFound(new { message = "Attachment not found" });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorageService, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FilePath,
            attachment.FileName, fallbackContentType: null,
            inline: false, cancellationToken);
    }

    /// <summary>Remove an attachment from an appraisal.</summary>
    [HttpDelete("{appraisalId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid appraisalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            // P9: the service decides between the uploader and the desk, and refuses after completion.
            var isDesk = await HoldsPolicyAsync(HrPermissions.PerformanceWritePolicy);
            var removed = await _appraisalService.DeleteAttachmentAsync(appraisalId, attachmentId, isDesk, cancellationToken);
            if (!removed) return NotFound(new { message = "Attachment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "deleting an appraisal attachment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId} from appraisal {AppraisalId}", attachmentId, appraisalId);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }

    /// <summary>
    /// Get all attachments for an appraisal
    /// </summary>
    [HttpGet("{appraisalId}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        // The list is entitled the same as the file. Gating the download but not the listing still
        // hands an outsider every filename, description and uploader on someone's appraisal.
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            var response = await _appraisalService.GetAttachmentsAsync(appraisalId, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    /// <summary>
    /// Get all appraisals for a specific employee (My Appraisals view)
    /// </summary>
    [HttpGet("my-appraisals/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<MyAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAppraisals(Guid employeeId, [FromQuery] string? cycleFilter = null)
    {
        if (!await CanAccessEmployeeRecordsAsync(employeeId, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            var response = await _appraisalService.GetMyAppraisalsAsync(employeeId, cycleFilter);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisals for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving appraisals");
        }
    }

    /// <summary>
    /// The signed-in employee's own appraisals. Peer work owed on other people's appraisals is a
    /// separate list — see <c>api/PeerEvaluations/me</c>.
    /// </summary>
    [HttpGet("my-appraisals/me")]
    [ProducesResponseType(typeof(IEnumerable<MyAppraisalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyOwnAppraisals([FromQuery] string? cycleFilter = null)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var response = await _appraisalService.GetMyAppraisalsAsync(employeeId, cycleFilter);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisals for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving appraisals");
        }
    }

    /// <summary>
    /// Get self-evaluation context for an appraisal
    /// </summary>
    [HttpGet("{appraisalId}/self-evaluation-context")]
    [ProducesResponseType(typeof(SelfEvaluationContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSelfEvaluationContext(Guid appraisalId)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            // P12/B2: the employee's entries are theirs until submitted, and the line manager's to read
            // only as the profile allows — the service applies AppraisalVisibility for this caller.
            var response = await _appraisalService.GetSelfEvaluationContextAsync(appraisalId, _currentUserService.EmployeeId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving self-evaluation context for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving self-evaluation context");
        }
    }

    /// <summary>
    /// Save or submit self-evaluation.
    ///
    /// ⚠ The evaluating employee is taken from the token, never from the body. The service only
    /// checks that <c>EmployeeId</c> matches the appraisal's subject, so a body-supplied id let
    /// any authenticated caller write — and submit — a self-evaluation on someone else's behalf.
    /// The body field is kept for compatibility but is overwritten here.
    /// </summary>
    [HttpPost("{appraisalId}/self-evaluation")]
    [ProducesResponseType(typeof(SelfEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveSelfEvaluation(Guid appraisalId, [FromBody] SaveSelfEvaluationDto saveDto)
    {
        try
        {
            // Ensure route parameter matches DTO
            if (appraisalId != saveDto.AppraisalId)
            {
                return BadRequest(new { message = "Appraisal ID mismatch" });
            }

            if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;
            saveDto.EmployeeId = employeeId;

            var response = await _appraisalService.SaveSelfEvaluationAsync(saveDto);

            if (!response.Success)
            {
                _logger.LogWarning("SaveSelfEvaluation returning 400 for appraisal {AppraisalId}: {Message}", appraisalId, response.Message);
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // A submission refused by the pipeline — the appraisal is not at the self-evaluation
            // step (performance closure B1). It used to fall to the catch-all below as a 500.
            return BusinessRuleRejected(ex, "submitting the self-evaluation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving self-evaluation for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, new SelfEvaluationResultDto
            {
                Success = false,
                Message = "An error occurred while saving self-evaluation"
            });
        }
    }

    /// <summary>
    /// Get read-only view of submitted self-evaluation
    /// </summary>
    [HttpGet("{appraisalId}/view-submitted-evaluation")]
    [ProducesResponseType(typeof(ViewSubmittedEvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetViewSubmittedEvaluation(Guid appraisalId)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            var response = await _appraisalService.GetViewSubmittedEvaluationAsync(appraisalId, _currentUserService.EmployeeId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving submitted evaluation for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving submitted evaluation");
        }
    }

    #endregion

    #region Manager Evaluation Endpoints

    /// <summary>
    /// Get team appraisal cycles for a manager
    /// </summary>
    [HttpGet("manager/{managerId:guid}/team-cycles")]
    [ProducesResponseType(typeof(IEnumerable<TeamAppraisalCycleSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamAppraisalCycles(Guid managerId)
    {
        // Another manager's team view is not this caller's to read; the /me twin needs no id.
        if (!await SelfOrPolicyAsync(managerId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var response = await _appraisalService.GetTeamAppraisalCyclesAsync(managerId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team appraisal cycles for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving team appraisal cycles");
        }
    }

    /// <summary>
    /// Team appraisal cycles for the signed-in manager. The client has no employee id of its
    /// own — the token is the only place it exists — so the /me pair is what the UI uses.
    /// </summary>
    [HttpGet("manager/me/team-cycles")]
    [ProducesResponseType(typeof(IEnumerable<TeamAppraisalCycleSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyTeamAppraisalCycles()
    {
        if (!TryGetEmployeeId(out var managerId, out var problem)) return problem!;

        try
        {
            var response = await _appraisalService.GetTeamAppraisalCyclesAsync(managerId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team appraisal cycles for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving team appraisal cycles");
        }
    }

    /// <summary>Team member appraisals in one cycle, for the signed-in manager.</summary>
    [HttpGet("manager/me/cycle/{cycleId:guid}/team-members")]
    [ProducesResponseType(typeof(IEnumerable<TeamMemberAppraisalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyTeamMemberAppraisals(Guid cycleId)
    {
        if (!TryGetEmployeeId(out var managerId, out var problem)) return problem!;

        try
        {
            var response = await _appraisalService.GetTeamMemberAppraisalsAsync(cycleId, managerId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team members for cycle {CycleId} and manager {ManagerId}", cycleId, managerId);
            return StatusCode(500, "An error occurred while retrieving team member appraisals");
        }
    }

    /// <summary>
    /// Get team member appraisals for a specific cycle
    /// </summary>
    [HttpGet("manager/{managerId:guid}/cycle/{cycleId:guid}/team-members")]
    [ProducesResponseType(typeof(IEnumerable<TeamMemberAppraisalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamMemberAppraisals(Guid cycleId, Guid managerId)
    {
        if (!await SelfOrPolicyAsync(managerId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var response = await _appraisalService.GetTeamMemberAppraisalsAsync(cycleId, managerId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team members for cycle {CycleId} and manager {ManagerId}", cycleId, managerId);
            return StatusCode(500, "An error occurred while retrieving team member appraisals");
        }
    }

    /// <summary>
    /// Get manager evaluation context for the currently authenticated manager (managerId resolved from JWT).
    /// </summary>
    [HttpGet("{appraisalId}/manager-evaluation-context")]
    [ProducesResponseType(typeof(ManagerEvaluationContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetManagerEvaluationContextForCurrentUser(Guid appraisalId)
    {
        var managerId = _currentUserService.EmployeeId;
        if (managerId == null)
            return BadRequest(new { message = "Could not determine current user's employee ID." });

        try
        {
            var response = await _appraisalService.GetManagerEvaluationContextAsync(appraisalId, managerId.Value);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving manager evaluation context for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving manager evaluation context");
        }
    }

    /// <summary>
    /// Get manager evaluation context for a specific appraisal
    /// </summary>
    [HttpGet("{appraisalId}/manager-evaluation-context/{managerId}")]
    [ProducesResponseType(typeof(ManagerEvaluationContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetManagerEvaluationContext(Guid appraisalId, Guid managerId)
    {
        // The service checks managerId against the appraisal, not against the caller — without
        // this, passing the true manager's id read out their evaluation context.
        if (!await SelfOrPolicyAsync(managerId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var response = await _appraisalService.GetManagerEvaluationContextAsync(appraisalId, managerId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving manager evaluation context for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving manager evaluation context");
        }
    }

    /// <summary>
    /// Save or submit manager evaluation.
    ///
    /// ⚠ The evaluating manager is taken from the token, never from the body. The service only
    /// checks the id against the employee's <c>ManagerId</c>, so a body-supplied id let any
    /// authenticated caller submit an evaluation as that manager. The body field is kept for
    /// compatibility but is overwritten here.
    /// </summary>
    [HttpPost("{appraisalId}/manager-evaluation")]
    [ProducesResponseType(typeof(ManagerEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SaveManagerEvaluation(Guid appraisalId, [FromBody] SaveManagerEvaluationDto saveDto)
    {
        try
        {
            // Ensure route parameter matches DTO
            if (appraisalId != saveDto.AppraisalId)
            {
                return BadRequest(new { message = "Appraisal ID mismatch" });
            }

            if (!TryGetEmployeeId(out var managerId, out var problem)) return problem!;
            saveDto.ManagerId = managerId;

            var response = await _appraisalService.SaveManagerEvaluationAsync(saveDto);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "saving the manager evaluation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving manager evaluation for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, new ManagerEvaluationResultDto
            {
                Success = false,
                Message = "An error occurred while saving manager evaluation"
            });
        }
    }

    /// <summary>
    /// Get detailed peer evaluations for manager review
    /// </summary>
    [HttpGet("{appraisalId}/manager-peer-evaluations")]
    [ProducesResponseType(typeof(ManagerPeerEvaluationReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetManagerPeerEvaluationReview(Guid appraisalId)
    {
        // Manager-or-desk only: this view names the peers, which anonymity hides from the appraisee.
        if (!await CanManageAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            var response = await _appraisalService.GetManagerPeerEvaluationReviewAsync(appraisalId, _currentUserService.EmployeeId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving peer evaluations for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving peer evaluations");
        }
    }

    #endregion

    #region Peer Nominations

    /// <summary>
    /// Get peer nomination summary for an appraisal
    /// </summary>
    [HttpGet("{appraisalId}/peer-nominations/summary")]
    [ProducesResponseType(typeof(PeerNominationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPeerNominationSummary(Guid appraisalId)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            // The reader decides what the list carries: the appraisee in Manager mode with anonymous
            // reviews is told the counts only (performance closure D-40).
            var response = await _peerNominationService.GetNominationSummaryAsync(appraisalId, _currentUserService.EmployeeId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving peer nomination summary for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving peer nomination summary");
        }
    }

    /// <summary>
    /// Create multiple peer nominations for an appraisal
    /// </summary>
    [HttpPost("{appraisalId}/peer-nominations/batch")]
    [ProducesResponseType(typeof(IEnumerable<PeerNominationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePeerNominationsBatch(Guid appraisalId, [FromBody] BatchCreatePeerNominationsDto batchDto)
    {
        // Nominating is the appraisee's (or their manager's / the desk's) act on THIS appraisal —
        // the service validates the list but never asked who was posting it.
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceWritePolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            // Ensure route parameter matches DTO
            if (appraisalId != batchDto.AppraisalId)
            {
                return BadRequest(new { message = "Appraisal ID mismatch" });
            }

            // P14: the nominator is whoever is posting — the service records it and applies the
            // cycle's nomination mode to it (it recorded every batch as the appraisee's).
            if (!TryGetEmployeeId(out var nominatorId, out var problem)) return problem!;

            var response = await _peerNominationService.BatchCreateAsync(batchDto, nominatorId);
            return CreatedAtAction(nameof(GetPeerNominationSummary), new { appraisalId }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "nominating peers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch peer nominations for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while creating peer nominations");
        }
    }

    /// <summary>
    /// Approve peer nominations and create evaluator evaluation records
    /// </summary>
    [HttpPost("{appraisalId}/peer-nominations/approve")]
    [ProducesResponseType(typeof(IEnumerable<PeerNominationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApprovePeerNominations(Guid appraisalId, [FromBody] ApprovePeerNominationsDto approveDto)
    {
        // Signing the peer list off is the manager's decision, not the appraisee's.
        if (!await CanManageAppraisalAsync(appraisalId, HrPermissions.PerformanceWritePolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            // Ensure route parameter matches DTO
            if (appraisalId != approveDto.AppraisalId)
            {
                return BadRequest(new { message = "Appraisal ID mismatch" });
            }

            var response = await _peerNominationService.ApproveNominationsAsync(approveDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "approving peer nominations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving peer nominations for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while approving peer nominations");
        }
    }

    /// <summary>
    /// Reject peer nominations
    /// </summary>
    [HttpPost("{appraisalId}/peer-nominations/reject")]
    [ProducesResponseType(typeof(IEnumerable<PeerNominationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectPeerNominations(Guid appraisalId, [FromBody] RejectPeerNominationsDto rejectDto)
    {
        if (!await CanManageAppraisalAsync(appraisalId, HrPermissions.PerformanceWritePolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            // Ensure route parameter matches DTO
            if (appraisalId != rejectDto.AppraisalId)
            {
                return BadRequest(new { message = "Appraisal ID mismatch" });
            }

            var response = await _peerNominationService.RejectNominationsAsync(rejectDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "rejecting peer nominations");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting peer nominations for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while rejecting peer nominations");
        }
    }

    #endregion

    #region HR Review

    /// <summary>
    /// Get HR review details for an appraisal.
    ///
    /// ⚠ The requester is resolved from the token. That id is what suppresses peer detail when
    /// the appraisee is the one looking and the cycle runs anonymous peer reviews — taking it
    /// from the query string meant an appraisee could unmask their peers simply by omitting it.
    /// </summary>
    [HttpGet("{id:guid}/hr-review")]
    [ProducesResponseType(typeof(HRReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHRReview(Guid id)
    {
        // The appraisee sees their own review (the service handles peer anonymity off the token);
        // beyond the parties it is a desk read.
        if (!await CanAccessAppraisalAsync(id, HrPermissions.PerformanceReadPolicy, HttpContext.RequestAborted)) return Forbid();

        try
        {
            var response = await _appraisalService.GetHRReviewAsync(id, _currentUserService.EmployeeId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HR review for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the HR review");
        }
    }

    /// <summary>
    /// Get list of appraisals for HR review
    /// </summary>
    [HttpGet("hr-review-list")]
    [ProducesResponseType(typeof(IEnumerable<HRReviewListItemDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetHRReviewList([FromQuery] Guid? cycleId = null, [FromQuery] string? status = null)
    {
        try
        {
            var response = await _appraisalService.GetHRReviewListAsync(cycleId, status);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HR review list");
            return StatusCode(500, "An error occurred while retrieving the HR review list");
        }
    }

    /// <summary>
    /// Hands an appraisal to HR: assigns the reviewer and opens the HR evaluation record.
    ///
    /// Submitting the manager evaluation does this automatically. This route exists for the case
    /// that failed — a tenant with no resolvable HR employee at the time — so HR can pick the
    /// appraisal up without redoing the manager's work. Returns false when HR review is not
    /// required by the cycle's settings, or when a reviewer is already assigned.
    /// </summary>
    [HttpPost("{id:guid}/progress-to-hr-review")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> ProgressToHRReview(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _appraisalService.ProgressToHRReviewAsync(id, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "progressing the appraisal to HR review");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error progressing appraisal {Id} to HR review", id);
            return StatusCode(500, "An error occurred while progressing the appraisal to HR review");
        }
    }

    /// <summary>
    /// Approve and finalize an appraisal. The signed-in HR user becomes the reviewer of record
    /// when nothing assigned one earlier.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(HRReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> ApproveAndFinalize(Guid id, [FromBody] ApproveAppraisalDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _appraisalService.ApproveAndFinalizeAsync(id, dto, _currentUserService.EmployeeId, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            // An HR officer does not sign off their own appraisal (E-a, the two-actor rule).
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // "Self evaluation must be completed", "at least N peer reviews" and the rest are
            // rules the reviewer hits routinely — 422 with the rule's own text, not a 400 body
            // shaped differently from every other HR endpoint.
            return BusinessRuleRejected(ex, "finalizing the appraisal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving appraisal {Id}", id);
            return StatusCode(500, "An error occurred while approving the appraisal");
        }
    }

    /// <summary>
    /// Return appraisal to manager for corrections
    /// </summary>
    [HttpPost("{id:guid}/return-to-manager")]
    [ProducesResponseType(typeof(HRReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> ReturnToManager(Guid id, [FromBody] ReturnAppraisalDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _appraisalService.ReturnToManagerAsync(id, dto, _currentUserService.EmployeeId, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            // The "HR remarks are required" guard is also an ArgumentException, so distinguish it
            // from a missing appraisal rather than reporting a validation failure as 404.
            return string.IsNullOrWhiteSpace(dto?.HRRemarks)
                ? BadRequest(new { message = ex.Message })
                : NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "returning the appraisal to its manager");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning appraisal {Id} to manager", id);
            return StatusCode(500, "An error occurred while returning the appraisal");
        }
    }

    /// <summary>
    /// Withdraws an appraisal from its cycle, with a reason (performance closure E-d1, D-10): from
    /// Draft, Active, or Governance before it is final (D-52). A withdrawn appraisal leaves every
    /// count and keeps what was written, without a score. Not the appraisee's own (403).
    /// </summary>
    /// <summary>
    /// Rebuilds the appraisal's form — its criterion snapshot — from its own template, before anyone has scored it
    /// (performance closure E-g2, D-86). HR's; refused on the officer's own appraisal (403), and on one already scored,
    /// not a draft or active, on a cycle that is not open, or with no approved template (422).
    /// </summary>
    [HttpPost("{id:guid}/rebuild-form")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> RebuildForm(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _configuration.RebuildSnapshotAsync(id, _currentUserService.EmployeeId, cancellationToken);
            return Ok(new { appraisalId = id, rows, message = $"The form was rebuilt from its template: {rows} row(s)." });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "rebuilding the appraisal's form");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding the form of appraisal {Id}", id);
            return StatusCode(500, new { message = "An error occurred while rebuilding the appraisal's form." });
        }
    }

    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType(typeof(PerformanceAppraisalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawAppraisalDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            await _withdrawals.WithdrawAsync(id, dto.Reason, cancellationToken);
            return Ok(await _appraisalService.GetByIdAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "withdrawing the appraisal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing appraisal {Id}", id);
            return StatusCode(500, "An error occurred while withdrawing the appraisal");
        }
    }

    /// <summary>
    /// Employee acknowledges receipt of finalized appraisal.
    ///
    /// ⚠ The acknowledging employee comes from the token. The service checks the id against the
    /// appraisal's subject, so a body-supplied id let any authenticated caller acknowledge —
    /// and thereby close — someone else's appraisal.
    /// </summary>
    [HttpPost("{id:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AcknowledgeAppraisal(Guid id, [FromBody] AcknowledgeAppraisalDto dto)
    {
        // dto.EmployeeId is deliberately ignored — see the remark above.
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            await _appraisalService.AcknowledgeAppraisalAsync(id, employeeId, dto?.Comments);
            return Ok(new { message = "Appraisal acknowledged successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "acknowledging the appraisal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging appraisal {Id}", id);
            return StatusCode(500, "An error occurred while acknowledging the appraisal");
        }
    }
    
    /// <summary>
    /// What the signed-in employee may appeal on this appraisal, and whether they still can.
    ///
    /// ⚠ The employee id used to come from the query string, and the service only checked it
    /// against the appraisal — so passing someone else's id read out their scored criteria.
    /// It comes from the token now, as everywhere else in the run.
    /// </summary>
    [HttpGet("{id:guid}/appeal-page-data")]
    [ProducesResponseType(typeof(AppealPageDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAppealPageData(Guid id)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var data = await _appraisalService.GetAppealPageDataAsync(id, employeeId);
            return Ok(data);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appeal page data for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving appeal page data");
        }
    }
    
    /// <summary>
    /// Submit an appeal for an appraisal
    /// </summary>
    [HttpPost("{id:guid}/submit-appeal")]
    [ProducesResponseType(typeof(AppraisalAppealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitAppeal(Guid id, [FromBody] SubmitAppealDto dto)
    {
        try
        {
            if (id != dto.AppraisalId)
                return BadRequest(new { message = "Appraisal ID mismatch" });

            var employeeId = _currentUserService.EmployeeId
                ?? throw new UnauthorizedAccessException("Employee record not linked to current user");

            var appeal = await _appraisalService.SubmitAppealAsync(dto, employeeId);
            return Ok(appeal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Appeals disabled, the window closed, not completed, already appealed — rules, answered
            // 422 like every gated appraisal write (performance closure B1).
            return BusinessRuleRejected(ex, "submitting an appeal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting appeal for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while submitting the appeal");
        }
    }
    
    /// <summary>
    /// The signed-in employee's own appeal, read-only. Same actor fix as the appeal page data.
    /// </summary>
    [HttpGet("{id:guid}/appeal-status")]
    [ProducesResponseType(typeof(AppealStatusViewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppealStatus(Guid id)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var appealStatus = await _appraisalService.GetAppealStatusAsync(id, employeeId);
            return Ok(appealStatus);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appeal status for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appeal status");
        }
    }

    /// <summary>
    /// Get list of all appraisal appeals with optional filtering.
    ///
    /// ⚠ Gated to HR: this is every appeal in the organisation, with the appellant's name and
    /// employee number. It was open to any authenticated caller.
    /// </summary>
    [HttpGet("appeals")]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<ActionResult<List<AppealListItemDto>>> GetAppealsList(
        [FromQuery] Guid? cycleId = null, 
        [FromQuery] AppraisalAppealStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var appeals = await _appraisalService.GetAppealsListAsync(cycleId, status, cancellationToken);
            return Ok(appeals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appeals list");
            return StatusCode(500, "An error occurred while retrieving the appeals list");
        }
    }

    /// <summary>
    /// HR's review of an appeal — and, once decided, its record, read-only (performance closure D-37:
    /// a decided appeal answered 400, so the queue's Decided tab opened to an error). The reader is
    /// passed on: what they see of each evaluation is the visibility rule's, and whether they may act
    /// the two-actor rule's.
    /// </summary>
    [HttpGet("{id:guid}/appeal-review")]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<ActionResult<AppealReviewDto>> GetAppealReview(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var reviewData = await _appraisalService.GetAppealReviewDataAsync(id, _currentUserService.EmployeeId, cancellationToken);
            return Ok(reviewData);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appeal review data for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appeal review data");
        }
    }

    /// <summary>
    /// Pick an appeal up: moves it from Submitted to UnderReview and records the reviewer, so a
    /// queue of untouched appeals is distinguishable from ones already being worked through.
    /// </summary>
    [HttpPost("{id:guid}/begin-appeal-review")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(AppraisalAppealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BeginAppealReview(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var reviewerId, out var problem)) return problem!;

        try
        {
            return Ok(await _appraisalService.BeginAppealReviewAsync(id, reviewerId, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            // The two-actor rule (performance closure D-35): not on an appeal the officer is party to.
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "picking up the appeal for review");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error beginning appeal review for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while picking up the appeal");
        }
    }

    /// <summary>
    /// Resolve an appraisal appeal
    /// </summary>
    [HttpPost("{id:guid}/resolve-appeal")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> ResolveAppeal(Guid id, [FromBody] ResolveAppealDto resolveDto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var reviewerId, out var problem)) return problem!;

        try
        {
            await _appraisalService.ResolveAppealAsync(id, resolveDto, reviewerId, cancellationToken);
            return Ok(new { message = "Appeal resolved successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            // The two-actor rule (performance closure D-35): not on an appeal the officer is party to.
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A decision the appeal cannot take (C4) — answered 422 like every gated appraisal write.
            return BusinessRuleRejected(ex, "deciding the appeal");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving appeal for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while resolving the appeal");
        }
    }

    /// <summary>
    /// Get post-remand review data including score comparisons for HR final decision
    /// </summary>
    [HttpGet("{id:guid}/post-remand-review")]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<ActionResult<PostRemandReviewDto>> GetPostRemandReview(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var reviewData = await _appraisalService.GetPostRemandReviewDataAsync(id, cancellationToken);
            return Ok(reviewData);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting post-remand review data for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the post-remand review data");
        }
    }

    /// <summary>
    /// Finalize a post-remand appeal with HR's final decision (Uphold or Reject)
    /// </summary>
    [HttpPost("{id:guid}/finalize-post-remand-appeal")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> FinalizePostRemandAppeal(Guid id, [FromBody] PostRemandFinalDecisionDto decisionDto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var reviewerId, out var problem)) return problem!;

        try
        {
            await _appraisalService.FinalizePostRemandAppealAsync(id, decisionDto, reviewerId, cancellationToken);
            return Ok(new { message = "Appeal finalized successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            // The two-actor rule (performance closure D-35).
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Not decidable yet (the re-evaluation is due and the deadline has not passed), or not
            // remanded — answered 422 like every gated appraisal write.
            return BusinessRuleRejected(ex, "finalising the appeal after the remand");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing post-remand appeal for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while finalizing the appeal");
        }
    }

    /// <summary>
    /// Move a remand's re-evaluation deadline (performance closure D-34): while the manager has not
    /// re-evaluated, to a later day — before the deadline or after it has passed. The manager is told.
    /// </summary>
    [HttpPost("{id:guid}/extend-remand")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExtendRemand(Guid id, [FromBody] ExtendRemandDeadlineDto extendDto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var reviewerId, out var problem)) return problem!;

        try
        {
            await _appraisalService.ExtendRemandDeadlineAsync(id, extendDto, reviewerId, cancellationToken);
            return Ok(new { message = "The re-evaluation deadline was moved" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "extending the remand deadline");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending the remand deadline for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while extending the deadline");
        }
    }

    /// <summary>
    /// Get employee read-only view of final appeal outcome
    /// </summary>
    [HttpGet("{id:guid}/appeal-outcome")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<EmployeeAppealOutcomeDto>> GetEmployeeAppealOutcome(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var employeeId = _currentUserService.EmployeeId
                ?? throw new UnauthorizedAccessException("Employee record not linked to current user");
            
            var outcome = await _appraisalService.GetEmployeeAppealOutcomeAsync(id, employeeId, cancellationToken);
            return Ok(outcome);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appeal outcome for appraisal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appeal outcome");
        }
    }
    #endregion
}