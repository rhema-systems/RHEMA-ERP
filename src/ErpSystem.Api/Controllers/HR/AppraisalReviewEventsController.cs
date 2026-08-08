using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalReviewEventsController : ControllerBase
{
    private readonly IAppraisalReviewEventService _reviewEventService;
    private readonly ILogger<AppraisalReviewEventsController> _logger;

    public AppraisalReviewEventsController(IAppraisalReviewEventService reviewEventService, ILogger<AppraisalReviewEventsController> logger)
    {
        _reviewEventService = reviewEventService;
        _logger = logger;
    }

    /// <summary>Get an appraisal review event by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal review event {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the review event");
        }
    }

    /// <summary>Get review events for an appraisal</summary>
    [HttpGet("by-appraisal/{appraisalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalReviewEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAppraisal(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.GetByAppraisalIdAsync(appraisalId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review events for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving review events");
        }
    }

    /// <summary>Get review events for an appraisal cycle</summary>
    [HttpGet("by-cycle/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalReviewEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCycle(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.GetByCycleIdAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review events for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving review events");
        }
    }

    /// <summary>Get review events of a specific type for an appraisal</summary>
    [HttpGet("by-appraisal/{appraisalId:guid}/type/{type}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalReviewEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(Guid appraisalId, ReviewEventType type, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.GetByTypeAsync(appraisalId, type, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review events of type {Type} for appraisal {AppraisalId}", type, appraisalId);
            return StatusCode(500, "An error occurred while retrieving review events");
        }
    }

    /// <summary>Create a new appraisal review event</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalReviewEventDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal review event");
            return StatusCode(500, "An error occurred while creating the review event");
        }
    }

    /// <summary>Update an existing appraisal review event</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalReviewEventDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal review event {Id}", id);
            return StatusCode(500, "An error occurred while updating the review event");
        }
    }

    /// <summary>Delete an appraisal review event</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Review event not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal review event {Id}", id);
            return StatusCode(500, "An error occurred while deleting the review event");
        }
    }

    /// <summary>Employee submits their self-assessment — moves status to EmployeeSubmitted</summary>
    [HttpPost("{eventId:guid}/submit")]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitEvent(Guid eventId, [FromBody] SubmitReviewEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.SubmitEventAsync(eventId, request.AchievementsSummary, request.ChallengesSummary, cancellationToken);
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
            _logger.LogError(ex, "Error submitting review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while submitting the review event");
        }
    }

    /// <summary>Manager closes a review event — moves status to Completed</summary>
    [HttpPost("{eventId:guid}/complete")]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteEvent(Guid eventId, [FromBody] CompleteReviewEventRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.CompleteEventAsync(eventId, request.Notes, request.ManagerNotes, cancellationToken);
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
            _logger.LogError(ex, "Error completing review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while completing the review event");
        }
    }

    // ── Full interim appraisal (Theme 7) ──────────────────────────────────

    private Guid GetEmployeeId()
        => Guid.TryParse(User.FindFirst("employee_id")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>Get the goals + context for scoring a full interim appraisal</summary>
    [HttpGet("{eventId:guid}/full-appraisal-context")]
    [ProducesResponseType(typeof(FullInterimAppraisalContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullAppraisalContext(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _reviewEventService.GetFullAppraisalContextAsync(eventId, cancellationToken));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading full-appraisal context for review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while loading the full appraisal");
        }
    }

    /// <summary>Score the period's goals and finalize a full interim appraisal (produces a period score)</summary>
    [HttpPost("{eventId:guid}/finalize-full-appraisal")]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FinalizeFullAppraisal(Guid eventId, [FromBody] FinalizeFullInterimAppraisalDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _reviewEventService.FinalizeFullAppraisalAsync(eventId, dto, GetEmployeeId(), cancellationToken));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing full appraisal for review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while finalizing the full appraisal");
        }
    }

    // ── Progress entries ──────────────────────────────────────────────────

    /// <summary>Record a goal progress entry during this review event</summary>
    [HttpPost("{eventId:guid}/progress")]
    [ProducesResponseType(typeof(GoalProgressEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordProgressEntry(Guid eventId, [FromBody] CreateGoalProgressEntryDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.RecordProgressEntryAsync(eventId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording progress entry for review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while recording the progress entry");
        }
    }

    /// <summary>Get goal progress entries for a review event</summary>
    [HttpGet("{eventId:guid}/progress")]
    [ProducesResponseType(typeof(IEnumerable<GoalProgressEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProgressEntries(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.GetProgressEntriesAsync(eventId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving progress entries for review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while retrieving progress entries");
        }
    }

    // ── Attachments ───────────────────────────────────────────────────────

    /// <summary>Add an attachment to a review event</summary>
    [HttpPost("{eventId:guid}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(Guid eventId, [FromBody] CreateAppraisalAttachmentDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.AddAttachmentAsync(eventId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding attachment to review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while adding the attachment");
        }
    }

    /// <summary>Get attachments for a review event</summary>
    [HttpGet("{eventId:guid}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.GetAttachmentsAsync(eventId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    /// <summary>Delete an attachment from a review event</summary>
    [HttpDelete("{eventId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid eventId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.DeleteAttachmentAsync(eventId, attachmentId, cancellationToken);
            if (!result) return NotFound(new { message = "Attachment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId} from review event {EventId}", attachmentId, eventId);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }
}

/// <summary>Request body for completing a review event</summary>
public record SubmitReviewEventRequest(string? AchievementsSummary, string? ChallengesSummary);
public record CompleteReviewEventRequest(string? Notes, string? ManagerNotes);
