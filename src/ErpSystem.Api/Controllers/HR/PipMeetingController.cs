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
[Authorize]
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
                return NotFound("Meeting not found");

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

        try
        {
            var createDto = new CreatePipReviewMeetingDto
            {
                PipId         = req.PipId,
                // Whoever books it holds it, unless they named someone else.
                ConductedById = req.ConductedById == Guid.Empty
                    ? _currentUserService.EmployeeId ?? Guid.Empty
                    : req.ConductedById,
                MeetingDate   = req.MeetingDate,
                ProgressNotes = string.Empty,
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

        try
        {
            var createDto = new CreatePipReviewMeetingDto
            {
                PipId            = model.PipId,
                MeetingDate      = model.MeetingDate,
                ConductedById    = model.ConductedById == Guid.Empty
                    ? _currentUserService.EmployeeId ?? Guid.Empty
                    : model.ConductedById,
                ProgressNotes    = model.ProgressNotes,
                IssuesDiscussed  = model.IssuesDiscussed,
                ActionsAgreed    = model.ActionsAgreed,
                EmployeeAttended = model.EmployeeAttended,
                EmployeeComments = model.EmployeeComments,
            };

            var meeting = await _pipService.AddReviewMeetingAsync(model.PipId, createDto);

            // Update goal progress if any goal updates specified
            await ApplyGoalUpdatesAsync(model.PipId, model.GoalUpdates);

            model.MeetingId = meeting.Id;
            return Ok(model);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
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
            var updateDto = new UpdatePipReviewMeetingDto
            {
                Id               = id,
                PipId            = model.PipId,
                MeetingDate      = model.MeetingDate,
                ConductedById    = model.ConductedById,
                ProgressNotes    = model.ProgressNotes,
                IssuesDiscussed  = model.IssuesDiscussed,
                ActionsAgreed    = model.ActionsAgreed,
                EmployeeAttended = model.EmployeeAttended,
                EmployeeComments = model.EmployeeComments,
            };

            await _pipService.UpdateReviewMeetingAsync(model.PipId, updateDto);

            // Update goal progress if any goal updates specified
            await ApplyGoalUpdatesAsync(model.PipId, model.GoalUpdates);

            return Ok(model);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating meeting {MeetingId}", id);
            return StatusCode(500, "An error occurred while updating the meeting");
        }
    }

    /// <summary>
    /// Mark a meeting as complete.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(PipMeetingFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteMeeting(Guid id, [FromBody] PipMeetingFormResponse model)
    {
        if (!await CanManageAsync(model.PipId)) return Forbid();

        try
        {
            var updateDto = new UpdatePipReviewMeetingDto
            {
                Id               = id,
                PipId            = model.PipId,
                MeetingDate      = model.MeetingDate,
                ConductedById    = model.ConductedById,
                ProgressNotes    = model.ProgressNotes,
                IssuesDiscussed  = model.IssuesDiscussed,
                ActionsAgreed    = model.ActionsAgreed,
                EmployeeAttended = model.EmployeeAttended,
                EmployeeComments = model.EmployeeComments,
            };

            await _pipService.UpdateReviewMeetingAsync(model.PipId, updateDto);

            // Apply goal progress updates
            await ApplyGoalUpdatesAsync(model.PipId, model.GoalUpdates);

            model.Status      = 2; // Completed
            model.CompletedOn = DateTime.UtcNow;
            return Ok(model);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing meeting {MeetingId}", id);
            return StatusCode(500, "An error occurred while completing the meeting");
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

            var response = new PipMeetingScheduleResponse
            {
                PipId          = pipId,
                CanScheduleMore = true,
                Meetings = meetings.Select((m, idx) => new PipMeetingListItemResponse
                {
                    MeetingId            = m.Id,
                    MeetingNumber        = idx + 1,
                    MeetingDate          = m.MeetingDate,
                    Status               = m.MeetingDate <= DateTime.UtcNow ? 2 : 1,
                    EmployeeAttended     = m.EmployeeAttended,
                    ConductedByName      = m.ConductedByName,
                    ProgressNotesPreview = m.ProgressNotes.Length > 120
                        ? m.ProgressNotes[..120] + "..."
                        : m.ProgressNotes,
                }).ToList(),
            };

            return Ok(response);
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
                return NotFound("Meeting not found");

            // The employee's right of reply is the one write they own on their plan — but it is
            // theirs, so it is gated on the plan like everything else rather than being open to
            // any authenticated caller.
            if (!await CanAccessAsync(meeting.PipId))
                return Forbid();

            var updateDto = new UpdatePipReviewMeetingDto
            {
                Id              = meetingId,
                PipId           = meeting.PipId,
                MeetingDate     = meeting.MeetingDate,
                EmployeeAttended = meeting.EmployeeAttended,
                ProgressNotes   = meeting.ProgressNotes,
                IssuesDiscussed = meeting.IssuesDiscussed,
                ActionsAgreed   = meeting.ActionsAgreed,
                EmployeeComments = req.Comment,
                ConductedById   = meeting.ConductedById,
            };

            await _pipService.UpdateReviewMeetingAsync(meeting.PipId, updateDto);
            return Ok(true);
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
            Status                 = meeting.MeetingDate <= DateTime.UtcNow ? 2 : 1,
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
