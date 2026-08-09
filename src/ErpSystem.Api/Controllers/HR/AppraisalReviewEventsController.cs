using ErpSystem.Api.Services.HR;
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

/// <summary>
/// Interim reviews — the quarterly / mid-year checkpoints between goal setting and the year-end
/// appraisal. How many exist, and whether each is a light-touch conversation or a full appraisal
/// against the period's goals, comes from the cycle's settings
/// (<c>ReviewFrequency</c>, <c>InterimReviewDepth</c>); the events themselves are generated when
/// the cycle generates its appraisals.
///
/// <para><b>Who can see one.</b> A review event carries an employee's achievements, difficulties
/// and — for a full interim appraisal — a period score. It is readable by HR, by the appraisee, and
/// by the appraisee's line manager. Everyone else is refused: before this the whole controller was
/// <c>[Authorize]</c> and nothing else, so any authenticated user could read, complete or delete
/// anyone's mid-year review by id.</para>
///
/// <para><b>Who does what.</b> Submitting the self-assessment is the appraisee's; completing the
/// review and finalizing a full interim appraisal are the manager's (or HR's). Creating and
/// deleting events is HR's — the enum documents <c>ReviewFrequency.Custom</c> as "HR creates review
/// events manually", and the rest are generated from the cycle.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalReviewEventsController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IAppraisalReviewEventService _reviewEventService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AppraisalReviewEventsController> _logger;

    public AppraisalReviewEventsController(
        IAppraisalReviewEventService reviewEventService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<AppraisalReviewEventsController> logger)
    {
        _reviewEventService = reviewEventService;
        _hrDocuments        = hrDocuments;
        _centralDocuments   = centralDocuments;
        _fileStorageService = fileStorageService;
        _db                 = db;
        _currentUserService = currentUserService;
        _logger             = logger;
    }

    private bool IsHr =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    /// <summary>
    /// Business rules — "the employee must submit their self-assessment first", "a goal progress
    /// update is required for every goal at this review" — come back as 422 carrying the rule's own
    /// message. Left to the generic handler, <c>UpdateAsync</c>'s "cannot update a completed review
    /// event" returned a 500 with a bare string body the client cannot read a message out of.
    /// Matches <c>EmployeeGoalsController</c> and <c>PerformanceImprovementPlansController</c>.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Review event rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// The caller's own employee id. Every actor on this controller comes from here — the previous
    /// code read a raw <c>employee_id</c> claim and defaulted to <c>Guid.Empty</c>, which is not a
    /// claim this solution issues.
    /// </summary>
    private Guid? CurrentEmployeeId =>
        _currentUserService.EmployeeId is Guid me && me != Guid.Empty ? me : null;

    /// <summary>
    /// Who this review event is about, plus their line manager — one query, reused by every gate.
    /// Null when the event does not exist in the caller's tenant, so the caller can 404 rather than
    /// 403 (a 403 would confirm the id exists somewhere).
    /// </summary>
    private async Task<(Guid EmployeeId, Guid? ManagerId)?> GetEventPartiesAsync(Guid eventId, CancellationToken ct)
    {
        if (_currentUserService.TenantId is not Guid tenantId) return null;

        var parties = await _db.Set<AppraisalReviewEvent>()
            .AsNoTracking()
            .Where(e => e.Id == eventId && e.TenantId == tenantId)
            .Select(e => new { e.Appraisal.EmployeeId, e.Appraisal.Employee.ManagerId })
            .FirstOrDefaultAsync(ct);

        return parties is null ? null : (parties.EmployeeId, parties.ManagerId);
    }

    /// <summary>HR, the appraisee, or the appraisee's line manager.</summary>
    private async Task<bool?> CanAccessEventAsync(Guid eventId, CancellationToken ct)
    {
        var parties = await GetEventPartiesAsync(eventId, ct);
        if (parties is null) return null;
        if (IsHr) return true;
        if (CurrentEmployeeId is not Guid me) return false;
        return parties.Value.EmployeeId == me || parties.Value.ManagerId == me;
    }

    /// <summary>As <see cref="CanAccessEventAsync"/> minus the appraisee — closing a review and
    /// scoring a period are the manager's side of it.</summary>
    private async Task<bool?> CanManageEventAsync(Guid eventId, CancellationToken ct)
    {
        var parties = await GetEventPartiesAsync(eventId, ct);
        if (parties is null) return null;
        if (IsHr) return true;
        if (CurrentEmployeeId is not Guid me) return false;
        return parties.Value.ManagerId == me;
    }

    /// <summary>Only the appraisee submits their own self-assessment; HR may do it on their behalf.</summary>
    private async Task<bool?> CanSubmitEventAsync(Guid eventId, CancellationToken ct)
    {
        var parties = await GetEventPartiesAsync(eventId, ct);
        if (parties is null) return null;
        if (IsHr) return true;
        if (CurrentEmployeeId is not Guid me) return false;
        return parties.Value.EmployeeId == me;
    }

    /// <summary>Turns a nullable entitlement into the right refusal, or null when the caller may proceed.</summary>
    private IActionResult? Refuse(bool? allowed, string notFoundMessage) => allowed switch
    {
        null  => NotFound(new { message = notFoundMessage }),
        false => Forbid(),
        _     => null,
    };

    private const string EventNotFound = "Review event not found";

    /// <summary>Get an appraisal review event by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(id, cancellationToken), EventNotFound) is { } refusal) return refusal;

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByAppraisal(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, cancellationToken)) return Forbid();

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

    /// <summary>
    /// The signed-in employee's own checkpoints, newest first. The client has no employee id of its
    /// own — this is how it gets a list without one.
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalReviewEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine([FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (CurrentEmployeeId is not Guid me) return Forbid();

        try
        {
            var result = await _reviewEventService.GetForEmployeeAsync(me, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving own review events");
            return StatusCode(500, "An error occurred while retrieving review events");
        }
    }

    /// <summary>Checkpoints for everyone reporting to the signed-in manager — their review queue.</summary>
    [HttpGet("team")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalReviewEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTeam([FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (CurrentEmployeeId is not Guid me) return Forbid();

        try
        {
            var result = await _reviewEventService.GetForManagerAsync(me, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team review events");
            return StatusCode(500, "An error occurred while retrieving review events");
        }
    }

    /// <summary>Same rule as a single event, keyed on the appraisal instead.</summary>
    private async Task<bool> CanAccessAppraisalAsync(Guid appraisalId, CancellationToken ct)
    {
        if (IsHr) return true;
        if (CurrentEmployeeId is not Guid me) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<PerformanceAppraisal>()
            .AsNoTracking()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .AnyAsync(a => a.EmployeeId == me || a.Employee.ManagerId == me, ct);
    }

    /// <summary>
    /// Every review event in a cycle — HR's org-wide view. A manager wanting their team's uses
    /// <c>by-appraisal</c> per report; this one spans the whole tenant.
    /// </summary>
    [HttpGet("by-cycle/{cycleId:guid}")]
    [Authorize(Roles = HrRoles)]
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByType(Guid appraisalId, ReviewEventType type, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, cancellationToken)) return Forbid();

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

    /// <summary>
    /// Create a review event. HR's, because this is cycle configuration — the generated ones come
    /// from the cycle's own <c>ReviewFrequency</c>.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
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
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "creating a review event");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal review event");
            return StatusCode(500, "An error occurred while creating the review event");
        }
    }

    /// <summary>Update an existing appraisal review event (reschedule, retitle, amend summaries)</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalReviewEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalReviewEventDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so a mismatch would silently edit a different row.
        if (id != updateDto.Id)
            return BadRequest(new { message = "The id in the route does not match the id in the body." });

        if (Refuse(await CanManageEventAsync(id, cancellationToken), EventNotFound) is { } refusal) return refusal;

        try
        {
            var result = await _reviewEventService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating a review event");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal review event {Id}", id);
            return StatusCode(500, "An error occurred while updating the review event");
        }
    }

    /// <summary>Delete an appraisal review event</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reviewEventService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = EventNotFound });
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitEvent(Guid eventId, [FromBody] SubmitReviewEventRequest request, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanSubmitEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

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
            return BusinessRuleRejected(ex, "submitting a review event");
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteEvent(Guid eventId, [FromBody] CompleteReviewEventRequest request, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanManageEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

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
            return BusinessRuleRejected(ex, "completing a review event");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while completing the review event");
        }
    }

    // ── Full interim appraisal (Theme 7) ──────────────────────────────────

    /// <summary>Get the goals + context for scoring a full interim appraisal</summary>
    [HttpGet("{eventId:guid}/full-appraisal-context")]
    [ProducesResponseType(typeof(FullInterimAppraisalContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullAppraisalContext(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> FinalizeFullAppraisal(Guid eventId, [FromBody] FinalizeFullInterimAppraisalDto dto, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanManageEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;
        if (CurrentEmployeeId is not Guid me)
            return Unauthorized("User employee context not found");

        try
        {
            return Ok(await _reviewEventService.FinalizeFullAppraisalAsync(eventId, dto, me, cancellationToken));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "finalizing a full interim appraisal");
        }
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordProgressEntry(Guid eventId, [FromBody] CreateGoalProgressEntryDto dto, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;
        if (CurrentEmployeeId is not Guid me)
            return Unauthorized("User employee context not found");

        try
        {
            var result = await _reviewEventService.RecordProgressEntryAsync(eventId, dto, me, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "recording a progress entry");
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProgressEntries(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

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

    /// <summary>
    /// Attach evidence to a review event, through the controlled-upload gate every other HR
    /// document family uses (scan, then central-DMS registration, rolled back if the record write
    /// fails).
    /// </summary>
    /// <remarks>
    /// This replaces a JSON endpoint that took a caller-supplied <c>filePath</c> and could not have
    /// worked in any case — see <c>AppraisalReviewEventService.AddAttachmentAsync</c>.
    /// </remarks>
    [HttpPost("{eventId:guid}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        Guid eventId, IFormFile file, [FromForm] string? description, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (CurrentEmployeeId is not Guid uploadedById)
            return Unauthorized("User employee context not found");

        if (_currentUserService.TenantId is not Guid tenantId ||
            !Guid.TryParse(_currentUserService.UserId, out var actorUserId))
            return Unauthorized("User context could not be resolved");

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId    = tenantId,
                ActorUserId = actorUserId,
                ActorName   = _currentUserService.UserName,
                Category    = ControlledFileUploadCategories.HrAppraisalAttachments,
                File        = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel      = "Interim review evidence",
                    SourceEntityType = "AppraisalReviewEvent",
                    SourceRecordId   = eventId,
                    Title            = Path.GetFileName(file.FileName),
                    DocumentType     = "ReviewEventAttachment",
                    ChangeSummary    = description
                }
            }, cancellationToken);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var result = await _reviewEventService.AddAttachmentAsync(
                eventId, uploadedById, document.OriginalFileName, document.FileSize, description,
                cancellationToken,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId);

            return StatusCode(201, result);
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(document, tenantId, actorUserId, cancellationToken);

            if (ex is ArgumentException)
                return NotFound(new { message = ex.Message });

            _logger.LogError(ex, "Error adding attachment to review event {EventId}", eventId);
            return StatusCode(500, "An error occurred while adding the attachment");
        }
    }

    /// <summary>Get attachments for a review event</summary>
    [HttpGet("{eventId:guid}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachments(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

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

    /// <summary>
    /// Streams a review-event attachment to a caller entitled to see it. The file lives outside the
    /// web root, so this endpoint is the only way to it.
    /// </summary>
    [HttpGet("{eventId:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid eventId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<AppraisalAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.ReviewEventId == eventId && a.TenantId == tenantId && !a.IsDeleted,
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

    /// <summary>Delete an attachment from a review event</summary>
    [HttpDelete("{eventId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid eventId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (Refuse(await CanAccessEventAsync(eventId, cancellationToken), EventNotFound) is { } refusal) return refusal;

        try
        {
            var attachment = await _reviewEventService.GetAttachmentAsync(eventId, attachmentId, cancellationToken);
            if (attachment == null)
                return NotFound(new { message = "Attachment not found" });

            // Evidence is the uploader's to withdraw; HR and the manager running the review may
            // also remove it. The appraisee cannot delete their manager's evidence.
            var mine = CurrentEmployeeId is Guid me && attachment.UploadedById == me;
            if (!mine && Refuse(await CanManageEventAsync(eventId, cancellationToken), EventNotFound) is { } denied)
                return denied;

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

/// <summary>Request body for submitting a review event</summary>
public record SubmitReviewEventRequest(string? AchievementsSummary, string? ChallengesSummary);

/// <summary>Request body for completing a review event</summary>
public record CompleteReviewEventRequest(string? Notes, string? ManagerNotes);
