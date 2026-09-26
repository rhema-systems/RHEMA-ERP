using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// A team's minute book, its periodic reviews, and how it is doing — slice F2.
/// </summary>
/// <remarks>
/// <para>Round 2, lane F2 (plan § 6.6). Shares the <c>api/hr/teams</c> prefix with
/// <c>TeamsController</c> (the register) and <c>TeamActivityController</c> (the charter, objectives
/// and tasks).</para>
///
/// <para><b>⚠ The gate here is thin for the same reason it is on F1's controller</b>, and it is not
/// an oversight: a team's lead is very often not an HR user, so a policy on these routes would lock
/// the one person who most needs them out of their own team's minute book. The vertical gate is
/// "any internal user"; the horizontal question is answered in <see cref="ITeamMeetingService"/>
/// against the record, through the shared <c>ITeamAccessGuard</c>.</para>
///
/// <para>The one exception is the sweep's run-now, which IS HR-gated: firing a tenant-wide sweep is
/// an administrative act, not something a team lead does to their own team.</para>
/// </remarks>
[ApiController]
[Route("api/hr/teams")]
[Authorize(Policy = "InternalOnly")]
[TeamActivityBusinessRules]
public class TeamMeetingController : ControllerBase
{
    private readonly ITeamMeetingService _service;
    private readonly ITeamReminderService _reminders;
    private readonly ICurrentUserService _currentUser;

    public TeamMeetingController(
        ITeamMeetingService service,
        ITeamReminderService reminders,
        ICurrentUserService currentUser)
    {
        _service = service;
        _reminders = reminders;
        _currentUser = currentUser;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    /// <summary>
    /// How this team is doing, in one read.
    /// </summary>
    /// <remarks>
    /// ⚠ One call rather than six, because the tiles have to agree with each other — an overdue
    /// count that disagrees with the list under it is worse than no tile at all.
    /// </remarks>
    [HttpGet("{teamId:guid}/dashboard")]
    [ProducesResponseType(typeof(TeamDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TeamDashboardDto>> GetDashboard(
        Guid teamId, CancellationToken cancellationToken)
        => Ok(await _service.GetDashboardAsync(teamId, cancellationToken));

    // ── Meetings ──────────────────────────────────────────────────────────────

    [HttpGet("{teamId:guid}/meetings")]
    [ProducesResponseType(typeof(IEnumerable<TeamMeetingListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMeetingListDto>>> GetMeetings(
        Guid teamId, CancellationToken cancellationToken)
        => Ok(await _service.GetMeetingsAsync(teamId, cancellationToken));

    [HttpGet("meetings/{id:guid}")]
    [ProducesResponseType(typeof(TeamMeetingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMeetingDetailDto>> GetMeeting(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetMeetingAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{teamId:guid}/meetings")]
    [ProducesResponseType(typeof(TeamMeetingDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TeamMeetingDetailDto>> CreateMeeting(
        Guid teamId, [FromBody] CreateTeamMeetingDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateMeetingAsync(teamId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetMeeting), new { id = created.Id }, created);
    }

    /// <summary>⚠ The attendee list is a REPLACE SET — an invitee omitted is an invitee removed.</summary>
    [HttpPut("meetings/{id:guid}")]
    [ProducesResponseType(typeof(TeamMeetingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamMeetingDetailDto>> UpdateMeeting(
        Guid id, [FromBody] UpdateTeamMeetingDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateMeetingAsync(id, dto, cancellationToken));
    }

    /// <summary>Records that it happened — the minutes, and who came.</summary>
    [HttpPost("meetings/{id:guid}/hold")]
    [ProducesResponseType(typeof(TeamMeetingDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamMeetingDetailDto>> HoldMeeting(
        Guid id, [FromBody] HoldTeamMeetingDto? dto, CancellationToken cancellationToken)
        => Ok(await _service.HoldMeetingAsync(id, dto ?? new HoldTeamMeetingDto(), cancellationToken));

    [HttpPost("meetings/{id:guid}/cancel")]
    [ProducesResponseType(typeof(TeamMeetingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamMeetingDetailDto>> CancelMeeting(
        Guid id, [FromBody] CancelMeetingRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CancelMeetingAsync(id, request?.Reason ?? string.Empty, cancellationToken));

    [HttpDelete("meetings/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteMeeting(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteMeetingAsync(id, cancellationToken);
        return NoContent();
    }

    // ── Decisions ─────────────────────────────────────────────────────────────

    [HttpPost("meetings/{meetingId:guid}/decisions")]
    [ProducesResponseType(typeof(TeamMeetingDecisionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamMeetingDecisionDto>> AddDecision(
        Guid meetingId, [FromBody] CreateTeamMeetingDecisionDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AddDecisionAsync(meetingId, dto, cancellationToken));
    }

    [HttpPut("decisions/{id:guid}")]
    [ProducesResponseType(typeof(TeamMeetingDecisionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamMeetingDecisionDto>> UpdateDecision(
        Guid id, [FromBody] UpdateTeamMeetingDecisionDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateDecisionAsync(id, dto, cancellationToken));
    }

    [HttpDelete("decisions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteDecision(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteDecisionAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Turns a decision into a team task.
    /// </summary>
    /// <remarks>
    /// ⚠ The assignee and the due date come from the DECISION, not from this payload. An action
    /// item that quietly acquired a different owner from the one the meeting named would leave the
    /// minute saying one thing and the board another.
    /// </remarks>
    [HttpPost("decisions/{id:guid}/raise-task")]
    [ProducesResponseType(typeof(TeamTaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamTaskDetailDto>> RaiseTaskFromDecision(
        Guid id, [FromBody] RaiseTaskFromDecisionDto? dto, CancellationToken cancellationToken)
        => Ok(await _service.RaiseTaskFromDecisionAsync(
            id, dto ?? new RaiseTaskFromDecisionDto(), cancellationToken));

    // ── Reviews ───────────────────────────────────────────────────────────────

    [HttpGet("{teamId:guid}/reviews")]
    [ProducesResponseType(typeof(IEnumerable<TeamReviewListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamReviewListDto>>> GetReviews(
        Guid teamId, CancellationToken cancellationToken)
        => Ok(await _service.GetReviewsAsync(teamId, cancellationToken));

    [HttpGet("reviews/{id:guid}")]
    [ProducesResponseType(typeof(TeamReviewDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamReviewDetailDto>> GetReview(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetReviewAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{teamId:guid}/reviews")]
    [ProducesResponseType(typeof(TeamReviewDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TeamReviewDetailDto>> CreateReview(
        Guid teamId, [FromBody] CreateTeamReviewDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateReviewAsync(teamId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetReview), new { id = created.Id }, created);
    }

    /// <summary>Edits a DRAFT. A submitted review is a record and cannot be changed.</summary>
    [HttpPut("reviews/{id:guid}")]
    [ProducesResponseType(typeof(TeamReviewDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamReviewDetailDto>> UpdateReview(
        Guid id, [FromBody] UpdateTeamReviewDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateReviewAsync(id, dto, cancellationToken));
    }

    /// <summary>Adds or updates the line for one objective, snapshotting its progress now.</summary>
    [HttpPut("reviews/{reviewId:guid}/lines")]
    [ProducesResponseType(typeof(TeamReviewLineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamReviewLineDto>> UpsertReviewLine(
        Guid reviewId, [FromBody] UpsertTeamReviewLineDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpsertReviewLineAsync(reviewId, dto, cancellationToken));
    }

    [HttpDelete("review-lines/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReviewLine(Guid id, CancellationToken cancellationToken)
        => await _service.DeleteReviewLineAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("reviews/{id:guid}/submit")]
    [ProducesResponseType(typeof(TeamReviewDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamReviewDetailDto>> SubmitReview(
        Guid id, CancellationToken cancellationToken)
        => Ok(await _service.SubmitReviewAsync(id, cancellationToken));

    /// <summary>The lead acknowledges it — says it was read, not that they agreed.</summary>
    [HttpPost("reviews/{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(TeamReviewDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamReviewDetailDto>> AcknowledgeReview(
        Guid id, [FromBody] AcknowledgeTeamReviewDto? dto, CancellationToken cancellationToken)
        => Ok(await _service.AcknowledgeReviewAsync(
            id, dto ?? new AcknowledgeTeamReviewDto(), cancellationToken));

    [HttpDelete("reviews/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteReviewAsync(id, cancellationToken);
        return NoContent();
    }

    // ── The sweep ─────────────────────────────────────────────────────────────
    //
    // ⚠ HR-gated, unlike everything above. Firing a TENANT-WIDE sweep is an administrative act, not
    // something a team lead does to their own team.

    /// <summary>
    /// Runs the reminder sweep now, for this tenant.
    /// </summary>
    /// <remarks>
    /// Shares its code path with the nightly host exactly — that is the whole reason the sweep logic
    /// is a scoped service rather than living inside the background service. A sweep reachable only
    /// from a timer cannot be tested, and two HR sweeps here were never running at all.
    /// </remarks>
    [HttpPost("reminders/run")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(TeamReminderRunResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamReminderRunResultDto>> RunReminderSweep(
        CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest(new { message = "Tenant context could not be resolved." });

        Guid? actor = Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null;
        return Ok(await _reminders.RunSweepForTenantAsync(tenantId, "Manual", actor, cancellationToken));
    }

    /// <summary>The recent runs — how anyone answers "did the sweep fire last night?".</summary>
    [HttpGet("reminders/runs")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<TeamReminderRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamReminderRunDto>>> GetReminderRuns(
        [FromQuery] int take = 20, CancellationToken cancellationToken = default)
        => Ok(await _reminders.GetRunsAsync(take, cancellationToken));

    [HttpGet("reminders/runs/{runId:guid}/dispatches")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<TeamReminderDispatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamReminderDispatchDto>>> GetReminderDispatches(
        Guid runId, CancellationToken cancellationToken)
        => Ok(await _reminders.GetDispatchesAsync(runId, cancellationToken));

    public sealed class CancelMeetingRequest
    {
        public string? Reason { get; set; }
    }
}
