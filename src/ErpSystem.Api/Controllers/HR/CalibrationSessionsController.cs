using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
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
/// Calibration sessions — the panel that reconciles managers' ratings across a unit before HR
/// signs the appraisals off.
///
/// <para><b>Reads</b> are open to any authenticated user, because the managers sitting on a panel
/// need the grid. <b>Writes</b> are HR/SuperAdmin: running a session, moving someone's rating and
/// committing the result are all HR actions, and committing lifts the calibration gate on every
/// appraisal in scope.</para>
///
/// <para>The actor is always taken from the token. The ported routes carried it as a path segment
/// (<c>…/open/{facilitatedById}</c>), which let any caller record the session as run by someone
/// else.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalibrationSessionsController : ControllerBase
{
    private readonly ICalibrationSessionService _calibrationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CalibrationSessionsController> _logger;

    public CalibrationSessionsController(
        ICalibrationSessionService calibrationService,
        ICurrentUserService currentUserService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ILogger<CalibrationSessionsController> logger)
    {
        _calibrationService = calibrationService;
        _currentUserService = currentUserService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorageService = fileStorageService;
        _db = db;
        _logger = logger;
    }

    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private bool TryGetEmployeeId(out Guid employeeId, out IActionResult? problem)
    {
        var id = _currentUserService.EmployeeId;
        if (id is null || id == Guid.Empty)
        {
            employeeId = Guid.Empty;
            problem = BadRequest(new { message = "Your account is not linked to an employee record, so it cannot facilitate a calibration session." });
            return false;
        }

        employeeId = id.Value;
        problem = null;
        return true;
    }

    /// <summary>
    /// Rules the service raises — wrong lifecycle state, an appraisal outside the session's
    /// scope, an empty session — answer 422 with the rule's own message, matching the rest of the
    /// appraisal controllers so a client can read <c>.message</c>.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Calibration rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>Get calibration sessions with pagination</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<CalibrationSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged calibration sessions");
            return StatusCode(500, "An error occurred while retrieving calibration sessions");
        }
    }

    /// <summary>Get a calibration session by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving calibration session {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the calibration session");
        }
    }

    /// <summary>Get calibration sessions for a cycle</summary>
    [HttpGet("by-cycle/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CalibrationSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCycle(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetByCycleIdAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving calibration sessions for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving calibration sessions");
        }
    }

    /// <summary>Create a new calibration session</summary>
    [HttpPost]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCalibrationSessionDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating calibration session");
            return StatusCode(500, "An error occurred while creating the calibration session");
        }
    }

    /// <summary>Update an existing calibration session</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCalibrationSessionDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so a mismatch would silently edit a different row.
        if (id != updateDto.Id)
            return BadRequest(new { message = "The id in the route does not match the id in the body." });

        try
        {
            var result = await _calibrationService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating the calibration session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating calibration session {Id}", id);
            return StatusCode(500, "An error occurred while updating the calibration session");
        }
    }

    /// <summary>Delete a calibration session</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Calibration session not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "deleting the calibration session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting calibration session {Id}", id);
            return StatusCode(500, "An error occurred while deleting the calibration session");
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    //
    // Pending → (open) → InProgress → (complete) → Completed → (commit).
    // Committing is a separate step from completing: closing the room and writing the agreed
    // ratings onto the appraisals are different decisions, and the second is irreversible.

    /// <summary>
    /// Opens the session and records the caller as its facilitator. Also links every appraisal in
    /// scope to the session, so their computed phase reads "calibration in progress".
    /// </summary>
    [HttpPost("{sessionId:guid}/open")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OpenSession(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _calibrationService.OpenSessionAsync(sessionId, employeeId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "opening the calibration session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while opening the calibration session");
        }
    }

    /// <summary>Stamps the session as having actually convened.</summary>
    [HttpPost("{sessionId:guid}/start")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartSession(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.StartSessionAsync(sessionId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "starting the calibration session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while starting the calibration session");
        }
    }

    /// <summary>Closes the session and notifies the panel that the ratings can be committed.</summary>
    [HttpPost("{sessionId:guid}/complete")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteSession(
        Guid sessionId, [FromBody] CompleteCalibrationSessionDto? dto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _calibrationService.CompleteSessionAsync(sessionId, employeeId, dto?.MeetingNotes, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "completing the calibration session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while completing the calibration session");
        }
    }

    // ── Participants ──────────────────────────────────────────────────────

    /// <summary>Add a participant to a calibration session</summary>
    [HttpPost("{sessionId:guid}/participants")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationParticipantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddParticipant(Guid sessionId, [FromBody] CreateCalibrationParticipantDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.AddParticipantAsync(sessionId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "adding a calibration participant");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding participant to calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while adding the participant");
        }
    }

    /// <summary>Get participants for a calibration session</summary>
    [HttpGet("{sessionId:guid}/participants")]
    [ProducesResponseType(typeof(IEnumerable<CalibrationParticipantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParticipants(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetParticipantsAsync(sessionId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving participants for calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while retrieving participants");
        }
    }

    /// <summary>Remove a participant from a calibration session</summary>
    [HttpDelete("{sessionId:guid}/participants/{participantId:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveParticipant(Guid sessionId, Guid participantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.RemoveParticipantAsync(sessionId, participantId, cancellationToken);
            if (!result) return NotFound(new { message = "Participant not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing participant {ParticipantId} from calibration session {SessionId}", participantId, sessionId);
            return StatusCode(500, "An error occurred while removing the participant");
        }
    }

    /// <summary>Record attendance for a participant</summary>
    [HttpPatch("{sessionId:guid}/participants/{participantId:guid}/attendance")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordAttendance(
        Guid sessionId, Guid participantId, [FromBody] RecordCalibrationAttendanceDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.RecordAttendanceAsync(sessionId, participantId, dto.Attended, cancellationToken);
            if (!result) return NotFound(new { message = "Participant not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording attendance for participant {ParticipantId} in calibration session {SessionId}", participantId, sessionId);
            return StatusCode(500, "An error occurred while recording attendance");
        }
    }

    // ── Rating adjustments ────────────────────────────────────────────────

    /// <summary>
    /// Record a panel decision. Omit <c>templateItemId</c> to restate the overall score, or supply
    /// one to move a single criterion. The adjuster is the caller.
    /// </summary>
    [HttpPost("{sessionId:guid}/adjustments")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationRatingAdjustmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRatingAdjustment(Guid sessionId, [FromBody] CreateCalibrationRatingAdjustmentDto dto, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _calibrationService.AddRatingAdjustmentAsync(sessionId, dto, employeeId, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "recording a calibration adjustment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding rating adjustment to calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while adding the rating adjustment");
        }
    }

    /// <summary>Get all rating adjustments for a calibration session</summary>
    [HttpGet("{sessionId:guid}/adjustments")]
    [ProducesResponseType(typeof(IEnumerable<CalibrationRatingAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRatingAdjustments(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetRatingAdjustmentsAsync(sessionId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rating adjustments for calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while retrieving rating adjustments");
        }
    }

    /// <summary>Get rating adjustments for a specific appraisal within a session</summary>
    [HttpGet("{sessionId:guid}/adjustments/appraisal/{appraisalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CalibrationRatingAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdjustmentsByAppraisal(Guid sessionId, Guid appraisalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetAdjustmentsByAppraisalAsync(sessionId, appraisalId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving adjustments for appraisal {AppraisalId} in calibration session {SessionId}", appraisalId, sessionId);
            return StatusCode(500, "An error occurred while retrieving rating adjustments");
        }
    }

    /// <summary>Update a rating adjustment</summary>
    [HttpPut("{sessionId:guid}/adjustments/{adjustmentId:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationRatingAdjustmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRatingAdjustment(Guid sessionId, Guid adjustmentId, [FromBody] UpdateCalibrationRatingAdjustmentDto dto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id.
        if (adjustmentId != dto.Id)
            return BadRequest(new { message = "The id in the route does not match the id in the body." });

        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _calibrationService.UpdateRatingAdjustmentAsync(sessionId, dto, employeeId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating a calibration adjustment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rating adjustment {AdjustmentId} in calibration session {SessionId}", adjustmentId, sessionId);
            return StatusCode(500, "An error occurred while updating the rating adjustment");
        }
    }

    /// <summary>Delete a rating adjustment</summary>
    [HttpDelete("{sessionId:guid}/adjustments/{adjustmentId:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRatingAdjustment(Guid sessionId, Guid adjustmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.DeleteRatingAdjustmentAsync(sessionId, adjustmentId, cancellationToken);
            if (!result) return NotFound(new { message = "Rating adjustment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rating adjustment {AdjustmentId} from calibration session {SessionId}", adjustmentId, sessionId);
            return StatusCode(500, "An error occurred while deleting the rating adjustment");
        }
    }

    /// <summary>
    /// Commit the session: write the agreed ratings onto the appraisals and lift the calibration
    /// gate on everyone in scope, adjusted or not.
    /// </summary>
    [HttpPost("{sessionId:guid}/apply-adjustments")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(CalibrationApplyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyAllAdjustments(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _calibrationService.ApplyAllAdjustmentsAsync(sessionId, employeeId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "committing the calibration session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying adjustments for calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while applying adjustments");
        }
    }

    // ── Matrix & Attachments ──────────────────────────────────────────────

    /// <summary>
    /// The calibration grid — every appraisal the session covers, with the manager's proposed
    /// score, the pre-calibration score and whatever the panel has changed so far.
    /// </summary>
    [HttpGet("{sessionId:guid}/matrix")]
    [ProducesResponseType(typeof(CalibrationMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalibrationMatrix(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetCalibrationMatrixAsync(sessionId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving calibration matrix for session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while retrieving the calibration matrix");
        }
    }

    /// <summary>
    /// Attach a file to a calibration session, through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// Replaces a JSON endpoint that took a caller-supplied <c>filePath</c> and could not have
    /// worked in any case — see <c>CalibrationSessionService.AddAttachmentAsync</c>.
    /// </remarks>
    [HttpPost("{sessionId:guid}/attachments")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> AddAttachment(
        Guid sessionId, IFormFile file, [FromForm] string? description, CancellationToken cancellationToken = default)
        => HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUserService, _logger, file,
            sourceEntityType: "CalibrationSession",
            sourceRecordId: sessionId,
            sourceLabel: "Calibration session attachment",
            documentType: "CalibrationSessionAttachment",
            description: description,
            persist: (uploadedById, document) => _calibrationService.AddAttachmentAsync(
                sessionId, uploadedById, document.OriginalFileName, document.FileSize, description,
                cancellationToken,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken);

    /// <summary>
    /// Streams a calibration attachment. Reads stay open to any authenticated user, matching the
    /// rest of this controller — the managers on a panel need the papers that go with the grid.
    /// </summary>
    [HttpGet("{sessionId:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid sessionId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<AppraisalAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.CalibrationSessionId == sessionId && a.TenantId == tenantId && !a.IsDeleted,
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

    /// <summary>
    /// An appraisal's frozen criteria for the calibration panel: weight, the manager's score, and
    /// whatever this session has already adjusted.
    /// </summary>
    /// <remarks>
    /// A read, so it follows this controller's rule that panellists can see the grid. Without it
    /// the calibration dialog could only move an appraisal's overall score — the API accepted
    /// per-criterion adjustments but nothing could enumerate the criteria to offer them, because
    /// the snapshot only came back inside a manager's or HR's own evaluation context.
    /// </remarks>
    [HttpGet("{sessionId:guid}/appraisals/{appraisalId:guid}/criteria")]
    [ProducesResponseType(typeof(IEnumerable<CalibrationCriterionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppraisalCriteria(Guid sessionId, Guid appraisalId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _calibrationService.GetAppraisalCriteriaAsync(sessionId, appraisalId, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving criteria for appraisal {AppraisalId} in session {SessionId}", appraisalId, sessionId);
            return StatusCode(500, "An error occurred while retrieving the appraisal's criteria");
        }
    }

    /// <summary>Get attachments for a calibration session</summary>
    [HttpGet("{sessionId:guid}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.GetAttachmentsAsync(sessionId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    /// <summary>Delete an attachment from a calibration session</summary>
    [HttpDelete("{sessionId:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid sessionId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.DeleteAttachmentAsync(sessionId, attachmentId, cancellationToken);
            if (!result) return NotFound(new { message = "Attachment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId} from calibration session {SessionId}", attachmentId, sessionId);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }
}
