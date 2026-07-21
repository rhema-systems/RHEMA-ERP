using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalibrationSessionsController : ControllerBase
{
    private readonly ICalibrationSessionService _calibrationService;
    private readonly ILogger<CalibrationSessionsController> _logger;

    public CalibrationSessionsController(ICalibrationSessionService calibrationService, ILogger<CalibrationSessionsController> logger)
    {
        _calibrationService = calibrationService;
        _logger = logger;
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
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCalibrationSessionDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating calibration session {Id}", id);
            return StatusCode(500, "An error occurred while updating the calibration session");
        }
    }

    /// <summary>Delete a calibration session</summary>
    [HttpDelete("{id:guid}")]
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting calibration session {Id}", id);
            return StatusCode(500, "An error occurred while deleting the calibration session");
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>Open a calibration session</summary>
    [HttpPost("{sessionId:guid}/open/{facilitatedById:guid}")]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OpenSession(Guid sessionId, Guid facilitatedById, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.OpenSessionAsync(sessionId, facilitatedById, cancellationToken);
            return Ok(result);
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
            _logger.LogError(ex, "Error opening calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while opening the calibration session");
        }
    }

    /// <summary>Start a calibration session</summary>
    [HttpPost("{sessionId:guid}/start")]
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
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while starting the calibration session");
        }
    }

    /// <summary>Complete a calibration session</summary>
    [HttpPost("{sessionId:guid}/complete/{completedById:guid}")]
    [ProducesResponseType(typeof(CalibrationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteSession(Guid sessionId, Guid completedById, [FromBody] string? meetingNotes = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.CompleteSessionAsync(sessionId, completedById, meetingNotes, cancellationToken);
            return Ok(result);
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
            _logger.LogError(ex, "Error completing calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while completing the calibration session");
        }
    }

    // ── Participants ──────────────────────────────────────────────────────

    /// <summary>Add a participant to a calibration session</summary>
    [HttpPost("{sessionId:guid}/participants")]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordAttendance(Guid sessionId, Guid participantId, [FromBody] bool attended, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.RecordAttendanceAsync(sessionId, participantId, attended, cancellationToken);
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

    /// <summary>Add a rating adjustment to a calibration session</summary>
    [HttpPost("{sessionId:guid}/adjustments")]
    [ProducesResponseType(typeof(CalibrationRatingAdjustmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRatingAdjustment(Guid sessionId, [FromBody] CreateCalibrationRatingAdjustmentDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.AddRatingAdjustmentAsync(sessionId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
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
    [ProducesResponseType(typeof(CalibrationRatingAdjustmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRatingAdjustment(Guid sessionId, Guid adjustmentId, [FromBody] UpdateCalibrationRatingAdjustmentDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.UpdateRatingAdjustmentAsync(sessionId, dto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rating adjustment {AdjustmentId} in calibration session {SessionId}", adjustmentId, sessionId);
            return StatusCode(500, "An error occurred while updating the rating adjustment");
        }
    }

    /// <summary>Delete a rating adjustment</summary>
    [HttpDelete("{sessionId:guid}/adjustments/{adjustmentId:guid}")]
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

    /// <summary>Apply all calibrated adjustments to linked appraisals</summary>
    [HttpPost("{sessionId:guid}/apply-adjustments/{appliedById:guid}")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyAllAdjustments(Guid sessionId, Guid appliedById, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _calibrationService.ApplyAllAdjustmentsAsync(sessionId, appliedById, cancellationToken);
            return Ok(new { appliedCount = count });
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
            _logger.LogError(ex, "Error applying adjustments for calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while applying adjustments");
        }
    }

    // ── Matrix & Attachments ──────────────────────────────────────────────

    /// <summary>Get the calibration matrix for a session</summary>
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

    /// <summary>Add an attachment to a calibration session</summary>
    [HttpPost("{sessionId:guid}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(Guid sessionId, [FromBody] CreateAppraisalAttachmentDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calibrationService.AddAttachmentAsync(sessionId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding attachment to calibration session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while adding the attachment");
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
