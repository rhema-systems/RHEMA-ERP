using ErpSystem.Api.Models;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The PIP review meeting form — the record of each monitoring conversation held during a plan.
///
/// <para>Everything here is scoped by the plan the meeting belongs to, and entitlement is
/// <see cref="PipAccess"/>'s: HR, the supervisor and the HR owner run the meetings; the employee
/// can read them and add their own comment, which is the one write they own.</para>
/// </summary>
[ApiController]
[Route("api/PipMeeting")]
[Authorize(Policy = "InternalOnly")]
public class PipMeetingController : ControllerBase
{
    private readonly IPerformanceImprovementPlanService _pipService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PipMeetingController> _logger;

    public PipMeetingController(
        IPerformanceImprovementPlanService pipService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<PipMeetingController> logger)
    {
        _pipService = pipService;
        _db         = db;
        _currentUserService = currentUserService;
        _logger     = logger;
    }

    private Task<bool> CanAccessAsync(Guid pipId, CancellationToken ct = default)
        => PipAccess.CanAccessAsync(this, _db, _currentUserService, pipId, ct);

    private Task<bool> CanManageAsync(Guid pipId, CancellationToken ct = default)
        => PipAccess.CanManageAsync(this, _db, _currentUserService, pipId, ct);

    /// <summary>
    /// A plan's rule refused the write — a closed plan, a cancelled or held meeting, a meeting not yet
    /// due (decision D-73). 422 with the rule's text, as the plan controller answers; these routes
    /// had no such catch, so every refusal came back as a 500.
    /// </summary>
    private IActionResult RuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("PIP meeting rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// Performance closure P13: whoever records a meeting holds it. The body's
    /// <c>conductedById</c> is not trusted — the forms post the loaded value back, and a caller
    /// could name anyone.
    /// </summary>
    private bool TryGetConductor(out Guid conductorId, out IActionResult? problem)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
        {
            conductorId = me;
            problem = null;
            return true;
        }

        conductorId = Guid.Empty;
        problem = BadRequest(new { message = "Your account is not linked to an employee record, so it cannot hold a review meeting." });
        return false;
    }

    /// <summary>
    /// Load an existing meeting by ID (with PIP context).
    /// </summary>
    [HttpGet("{meetingId:guid}")]
    [ProducesResponseType(typeof(PipMeetingFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeeting(Guid meetingId, [FromQuery] Guid pipId)
    {
        if (!await CanAccessAsync(pipId)) return Forbid();

        try
        {
            var meetings = (await _pipService.GetReviewMeetingsAsync(pipId)).ToList();
            var meeting  = meetings.FirstOrDefault(m => m.Id == meetingId);
            if (meeting == null)
                return NotFound(new { message = "Meeting not found" });

            var pip  = await _pipService.GetByIdAsync(pipId);
            var goals = (await _pipService.GetPipGoalsAsync(pipId)).ToList();

            var meetingNumber = meetings
                .OrderBy(m => m.MeetingDate)
                .ToList()
                .FindIndex(m => m.Id == meetingId) + 1;

            var response = BuildMeetingFormResponse(meeting, pip, goals,
                meetingNumber, meetings.Count);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading meeting {MeetingId}", meetingId);
            return StatusCode(500, "An error occurred while loading the meeting");
        }
    }

    /// <summary>
    /// Prepare a blank meeting form (pre-populated with PIP context).
    /// </summary>
    [HttpGet("prepare")]
    [ProducesResponseType(typeof(PipMeetingFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Prepare([FromQuery] Guid pipId)
    {
        if (!await CanManageAsync(pipId)) return Forbid();

        try
        {
            var pip      = await _pipService.GetByIdAsync(pipId);
            var goals    = (await _pipService.GetPipGoalsAsync(pipId)).ToList();
            var meetings = (await _pipService.GetReviewMeetingsAsync(pipId)).ToList();

            var response = new PipMeetingFormResponse
            {
                PipId                  = pip.Id,
                PipNumber              = pip.PipNumber,
                PipStatus              = pip.Status,
                EmployeeId             = pip.EmployeeId,
                EmployeeName           = pip.EmployeeName,
                PipStartDate           = pip.StartDate,
                PipEndDate             = pip.EndDate,
                MeetingDate            = DateTime.Today,
                MeetingNumber          = meetings.Count + 1,
                TotalScheduledMeetings = meetings.Count + 1,
                Status                 = 1, // Scheduled
                EmployeeAttended       = true,
                GoalUpdates = goals.Select(g => new PipGoalMeetingUpdateResponse
                {
                    GoalId               = g.Id,
                    GoalTitle            = g.Title,
                    SuccessCriteria      = g.SuccessCriteria,
                    DueDate              = g.DueDate,
                    CurrentProgressPercent = g.ProgressPercent,
                    CurrentStatus        = g.Status,
                }).ToList(),
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing new meeting for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while preparing the meeting");
        }
    }

    /// <summary>
    /// Schedule a new meeting (quick-create with minimal fields).
    /// </summary>
    [HttpPost("schedule")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Schedule([FromBody] ScheduleMeetingRequest req)
    {
        if (!await CanManageAsync(req.PipId)) return Forbid();
        if (!TryGetConductor(out var conductorId, out var problem)) return problem!;

        try
        {
            var createDto = new CreatePipReviewMeetingDto
            {
                PipId         = req.PipId,
                // Whoever books it holds it (P13).
                ConductedById = conductorId,
                MeetingDate   = req.MeetingDate,
                ProgressNotes = string.Empty,
                // The record form's default; attendance means something once the meeting is Held (D-73),
                // and the screens show it for held meetings only.
                EmployeeAttended = true,
            };

            var meeting = await _pipService.AddReviewMeetingAsync(req.PipId, createDto);
            return Ok(meeting.Id);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("PIP meeting rule rejected while scheduling: {Message}", ex.Message);
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling meeting for PIP {PipId}", req.PipId);
            return StatusCode(500, "An error occurred while scheduling the meeting");
        }
    }

    /// <summary>
    /// Create a new meeting (full form save).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PipMeetingFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMeeting([FromBody] PipMeetingFormResponse model)
    {
        if (model.PipId == Guid.Empty)
            return BadRequest("PipId is required");
        if (!await CanManageAsync(model.PipId)) return Forbid();
        if (!TryGetConductor(out var conductorId, out var problem)) return problem!;

        try
        {
            // P13: the conductor is the caller, and the employee's reply is theirs to write.
            var createDto = new CreatePipReviewMeetingDto
            {
                PipId            = model.PipId,
                MeetingDate      = model.MeetingDate,
                ConductedById    = conductorId,
                ProgressNotes    = model.ProgressNotes ?? string.Empty,
                IssuesDiscussed  = model.IssuesDiscussed,
                ActionsAgreed    = model.ActionsAgreed,
                EmployeeAttended = model.EmployeeAttended,
            };

            // Booked as Scheduled (decision D-73); Record meeting makes it Held.
            var meeting = await _pipService.AddReviewMeetingAsync(model.PipId, createDto);

            // Update goal progress if any goal updates specified
            await ApplyGoalUpdatesAsync(model.PipId, model.GoalUpdates);

            model.MeetingId = meeting.Id;
            model.Status    = (int)meeting.Status;
            return Ok(model);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A plan not in force — a draft, out for approval, or closed — takes no meeting: a rule
            // (422). This answered 500.
            return RuleRejected(ex, "creating a meeting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating meeting for PIP {PipId}", model.PipId);
            return StatusCode(500, "An error occurred while creating the meeting");
        }
    }

    /// <summary>
    /// Update an existing meeting.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PipMeetingFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMeeting(Guid id, [FromBody] PipMeetingFormResponse model)
    {
        if (!await CanManageAsync(model.PipId)) return Forbid();

        try
        {
            // P13: the supervisor's record only. The form posts back the stored conductor and the
            // employee's reply; neither is taken from it.
            var updateDto = new UpdatePipReviewMeetingDto
            {
                Id               = id,
                PipId            = model.PipId,
                MeetingDate      = model.MeetingDate,
                ProgressNotes    = model.ProgressNotes ?? string.Empty,
                IssuesDiscussed  = model.IssuesDiscussed,
                ActionsAgreed    = model.ActionsAgreed,
                EmployeeAttended = model.EmployeeAttended,
            };

            var saved = await _pipService.UpdateReviewMeetingAsync(model.PipId, updateDto);

            // Update goal progress if any goal updates specified
            await ApplyGoalUpdatesAsync(model.PipId, model.GoalUpdates);

            model.Status = (int)saved.Status;
            return Ok(model);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return RuleRejected(ex, "updating a meeting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating meeting {MeetingId}", id);
            return StatusCode(500, "An error occurred while updating the meeting");
        }
    }

    /// <summary>
    /// Record meeting: the supervisor's record of a meeting that took place, which stores it as Held
    /// (decision D-73). It used to save the form and put "completed" in the response only.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(PipMeetingFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteMeeting(Guid id, [FromBody] PipMeetingFormResponse model)
    {
        if (!await CanManageAsync(model.PipId)) return Forbid();

        try
        {
            // P13: the supervisor's record only. The form posts back the stored conductor and the
            // employee's reply; neither is taken from it.
            var updateDto = new UpdatePipReviewMeetingDto
            {
                Id               = id,
                PipId            = model.PipId,
                MeetingDate      = model.MeetingDate,
                ProgressNotes    = model.ProgressNotes ?? string.Empty,
                IssuesDiscussed  = model.IssuesDiscussed,
                ActionsAgreed    = model.ActionsAgreed,
                EmployeeAttended = model.EmployeeAttended,
            };

            var held = await _pipService.RecordReviewMeetingAsync(model.PipId, updateDto);

            // Apply goal progress updates
            await ApplyGoalUpdatesAsync(model.PipId, model.GoalUpdates);

            model.Status      = (int)held.Status;
            model.CompletedOn = DateTime.UtcNow;
            return Ok(model);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return RuleRejected(ex, "recording a meeting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing meeting {MeetingId}", id);
            return StatusCode(500, "An error occurred while completing the meeting");
        }
    }

    /// <summary>
    /// Cancel a booked meeting that will not take place (decision D-73). A held meeting is the plan's
    /// record and is not cancelled; a cancelled one takes no more writes.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(PipReviewMeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelMeeting(Guid id, [FromQuery] Guid pipId)
    {
        if (!await CanManageAsync(pipId)) return Forbid();

        try
        {
            return Ok(await _pipService.CancelReviewMeetingAsync(pipId, id));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return RuleRejected(ex, "cancelling a meeting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling meeting {MeetingId}", id);
            return StatusCode(500, new { message = "An error occurred while cancelling the meeting." });
        }
    }

    /// <summary>
    /// Get the full meeting schedule for a PIP.
    /// </summary>
    [HttpGet("schedule/{pipId:guid}")]
    [ProducesResponseType(typeof(PipMeetingScheduleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule(Guid pipId)
    {
        if (!await CanAccessAsync(pipId)) return Forbid();

        try
        {
            var meetings = (await _pipService.GetReviewMeetingsAsync(pipId))
                .OrderBy(m => m.MeetingDate)
                .ToList();
            var pip = await _pipService.GetByIdAsync(pipId);

            var response = new PipMeetingScheduleResponse
            {
                PipId          = pipId,
                // Meetings are booked while the plan is in force (decision D-73).
                CanScheduleMore = pip.Status is PipStatus.Active or PipStatus.InProgress,
                Meetings = meetings.Select((m, idx) => new PipMeetingListItemResponse
                {
                    MeetingId            = m.Id,
                    MeetingNumber        = idx + 1,
                    MeetingDate          = m.MeetingDate,
                    Status               = (int)m.Status,
                    EmployeeAttended     = m.EmployeeAttended,
                    ConductedByName      = m.ConductedByName,
                    ProgressNotesPreview = m.ProgressNotes.Length > 120
                        ? m.ProgressNotes[..120] + "..."
                        : m.ProgressNotes,
                }).ToList(),
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading meeting schedule for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while loading the meeting schedule");
        }
    }

    /// <summary>
    /// Add an employee comment to a meeting.
    /// </summary>
    [HttpPost("{meetingId:guid}/comment")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        Guid meetingId, [FromBody] EmployeeCommentRequest req)
    {
        try
        {
            var meeting = await _pipService.GetReviewMeetingByIdAsync(meetingId);
            if (meeting == null)
                return NotFound(new { message = "Meeting not found" });

            // The employee's right of reply is the one write they own on their plan, and it is
            // theirs alone (performance closure P13): not the supervisor's, not the HR owner's, not
            // a performance-Read holder's "on their behalf". The service holds the rule.
            if (!await CanAccessAsync(meeting.PipId))
                return Forbid();
            if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty)
                return Forbid();

            await _pipService.SetEmployeeCommentsAsync(meeting.PipId, meetingId, me, req.Comment);
            return Ok(true);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A closed plan or a cancelled meeting (decision D-73).
            return RuleRejected(ex, "adding a reply");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding employee comment for meeting {MeetingId}", meetingId);
            return StatusCode(500, "An error occurred while adding the comment");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task ApplyGoalUpdatesAsync(Guid pipId, List<PipGoalMeetingUpdateResponse> goalUpdates)
    {
        foreach (var update in goalUpdates.Where(g => g.CurrentProgressPercent.HasValue))
        {
            try
            {
                await _pipService.UpdatePipGoalProgressAsync(
                    pipId,
                    update.GoalId,
                    update.CurrentProgressPercent!.Value,
                    null,
                    update.CurrentStatus);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update progress for goal {GoalId}", update.GoalId);
            }
        }
    }

    private static PipMeetingFormResponse BuildMeetingFormResponse(
        PipReviewMeetingDto meeting,
        PerformanceImprovementPlanDto pip,
        IEnumerable<PipGoalDto> goals,
        int meetingNumber,
        int totalMeetings)
    {
        return new PipMeetingFormResponse
        {
            MeetingId              = meeting.Id,
            PipId                  = meeting.PipId,
            PipNumber              = pip.PipNumber,
            PipStatus              = pip.Status,
            EmployeeId             = pip.EmployeeId,
            EmployeeName           = pip.EmployeeName,
            PipStartDate           = pip.StartDate,
            PipEndDate             = pip.EndDate,
            MeetingNumber          = meetingNumber,
            TotalScheduledMeetings = totalMeetings,
            MeetingDate            = meeting.MeetingDate,
            ConductedById          = meeting.ConductedById,
            ConductedByName        = meeting.ConductedByName,
            EmployeeAttended       = meeting.EmployeeAttended,
            ProgressNotes          = meeting.ProgressNotes,
            IssuesDiscussed        = meeting.IssuesDiscussed,
            ActionsAgreed          = meeting.ActionsAgreed,
            EmployeeComments       = meeting.EmployeeComments,
            Status                 = (int)meeting.Status,
            GoalUpdates = goals.Select(g => new PipGoalMeetingUpdateResponse
            {
                GoalId                 = g.Id,
                GoalTitle              = g.Title,
                SuccessCriteria        = g.SuccessCriteria,
                DueDate                = g.DueDate,
                CurrentProgressPercent = g.ProgressPercent,
                CurrentStatus          = g.Status,
            }).ToList(),
        };
    }
}
