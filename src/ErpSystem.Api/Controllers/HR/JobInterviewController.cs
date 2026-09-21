using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Interview sessions — scheduling, the panel, the question plan, and the panel's scorecards.
///
/// <para><b>Authorization is enforced in the service, not by role attributes here.</b> That is the
/// difference between this controller and <see cref="JobApplicationController"/>. An interview cannot be
/// HR-only: the people who have to open it, read the questions and file a scorecard are ordinary
/// employees who happen to sit on that panel. Nor can it be open to any authenticated employee — which
/// is what the bare <c>[Authorize]</c> here used to mean — because it carries the candidate's contact
/// details, the panel's private comments and the hire recommendation. The rule is therefore per record
/// ("HR, or a panelist on <i>this</i> interview") and lives in <c>JobInterviewService</c>, where it
/// holds no matter which route reaches it. <see cref="RecruitmentBusinessRulesAttribute"/> turns the
/// service's refusals into 403s with their own message.</para>
///
/// <para>The two anonymous confirmation endpoints at the bottom are reached from an emailed link by
/// candidates and external panelists, who have no login at all; they are authorised by a single-use
/// token and rate-limited.</para>
/// </summary>
[ApiController]
[Route("api/job-interviews")]
[Authorize(Policy = "InternalOnly")]
[RecruitmentBusinessRules]
public class JobInterviewController : ControllerBase
{
    private readonly IJobInterviewService _service;
    private readonly ICurrentUserService _currentUser;

    public JobInterviewController(IJobInterviewService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // INTERVIEW QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobInterviewDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{interviewNumber}")]
    public async Task<ActionResult<JobInterviewDto?>> GetByInterviewNumber(string interviewNumber)
        => Ok(await _service.GetByInterviewNumberAsync(interviewNumber));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<JobInterviewDetailDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithFullDetailsAsync(id));

    [HttpGet("vacancy/{vacancyId:guid}")]
    public async Task<ActionResult<IEnumerable<JobInterviewSummaryDto>>> GetByVacancy(Guid vacancyId)
        => Ok(await _service.GetByVacancyIdAsync(vacancyId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<JobInterviewSummaryDto>>> GetByStatus(JobInterviewStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<JobInterviewSummaryDto>>> GetByDateRange(
        [FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetByDateRangeAsync(from, to));

    [HttpGet("vacancy/{vacancyId:guid}/round/{round:int}")]
    public async Task<ActionResult<IEnumerable<JobInterviewSummaryDto>>> GetByRound(Guid vacancyId, int round)
        => Ok(await _service.GetByRoundAsync(vacancyId, round));

    /// <summary>
    /// Advisory availability check for a proposed interview slot: overlapping interviews the panelists
    /// already sit on, plus approved/pending leave and travel. Non-blocking — surfaced as a UI warning.
    /// </summary>
    [HttpGet("panelist-availability")]
    public async Task<ActionResult<PanelistAvailabilityCheckDto>> CheckPanelistAvailability(
        [FromQuery] List<Guid> panelistIds,
        [FromQuery] List<Guid> externalPanelistIds,
        [FromQuery] DateOnly date,
        [FromQuery] TimeSpan start,
        [FromQuery] TimeSpan end,
        [FromQuery] Guid? excludeInterviewId,
        CancellationToken ct)
        => Ok(await _service.CheckPanelistAvailabilityAsync(
            panelistIds ?? new List<Guid>(), externalPanelistIds ?? new List<Guid>(),
            date, start, end, excludeInterviewId, ct));

    // =========================================================================
    // INTERVIEW CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<JobInterviewDto>> Create([FromBody] CreateJobInterviewDto dto)
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
    public async Task<ActionResult<JobInterviewDto>> Update(Guid id, [FromBody] UpdateJobInterviewDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // INTERVIEW WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleJobInterviewDto dto)
    {
        // The service keys off the body's id, so a mismatch silently rescheduled a different interview.
        dto.InterviewId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RescheduleAsync(dto, employeeId.Value);
        return Ok(new { message = "Interview rescheduled." });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelJobInterviewDto dto)
    {
        dto.InterviewId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CancelAsync(dto, employeeId.Value);
        return Ok(new { message = "Interview cancelled." });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.CompleteAsync(id, employeeId.Value);
        return Ok(new { message = "Interview marked complete." });
    }

    // =========================================================================
    // PANELISTS (INTERNAL)
    // =========================================================================

    [HttpGet("{interviewId:guid}/panelists")]
    public async Task<ActionResult<IEnumerable<JobInterviewPanelistDto>>> GetPanelists(Guid interviewId)
        => Ok(await _service.GetPanelistsAsync(interviewId));

    [HttpPost("{interviewId:guid}/panelists")]
    public async Task<ActionResult<JobInterviewPanelistDto>> AddPanelist(
        Guid interviewId, [FromBody] AddJobInterviewPanelistDto dto)
    {
        dto.JobInterviewId = interviewId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddPanelistAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpDelete("panelists/{panelistId:guid}")]
    public async Task<IActionResult> RemovePanelist(Guid panelistId)
    {
        await _service.RemovePanelistAsync(panelistId);
        return NoContent();
    }

    [HttpPut("panelists/{panelistId:guid}")]
    public async Task<ActionResult<JobInterviewPanelistDto>> UpdatePanelist(
        Guid panelistId, [FromBody] UpdateJobInterviewPanelistDto dto)
    {
        if (panelistId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdatePanelistAsync(dto, employeeId.Value));
    }

    [HttpPost("panelists/{panelistId:guid}/confirm")]
    public async Task<IActionResult> ConfirmPanelist(Guid panelistId)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ConfirmPanelistAsync(panelistId, employeeId.Value);
        return Ok(new { message = "Panelist confirmed." });
    }

    [HttpPost("panelists/{panelistId:guid}/attendance")]
    public async Task<IActionResult> RecordPanelistAttendance(
        Guid panelistId, [FromBody] RecordPanelistAttendanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordPanelistAttendanceAsync(panelistId, dto.Attended, dto.NoShowReason, employeeId.Value);
        return Ok(new { message = "Panelist attendance recorded." });
    }

    [HttpGet("employee/{employeeId:guid}/panelist-slots")]
    public async Task<ActionResult<IEnumerable<JobInterviewPanelistDto>>> GetInterviewsByPanelist(Guid employeeId)
        => Ok(await _service.GetInterviewsByPanelistAsync(employeeId));

    /// <summary>
    /// The caller's own panel assignments. The id-bearing route above is HR's; a panelist reaching their
    /// own diary through it would have to fetch and pass their own employee id, which is exactly the
    /// shape that produced this module's authorization holes.
    /// </summary>
    [HttpGet("me/panelist-slots")]
    public async Task<ActionResult<IEnumerable<JobInterviewPanelistDto>>> GetMyPanelistSlots()
        => Ok(await _service.GetMyPanelistSlotsAsync());

    // =========================================================================
    // EXTERNAL PANELISTS
    // =========================================================================

    [HttpGet("{interviewId:guid}/external-panelists")]
    public async Task<ActionResult<IEnumerable<JobInterviewExternalPanelistDto>>> GetExternalPanelists(Guid interviewId)
        => Ok(await _service.GetExternalPanelistsAsync(interviewId));

    [HttpPost("external-panelists/{extPanelistId:guid}/attendance")]
    public async Task<IActionResult> RecordExternalPanelistAttendance(
        Guid extPanelistId, [FromBody] RecordExternalPanelistAttendanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordExternalPanelistAttendanceAsync(extPanelistId, dto.Attended, dto.NoShowReason, employeeId.Value);
        return Ok(new { message = "External panelist attendance recorded." });
    }

    [HttpPost("{interviewId:guid}/external-panelists")]
    public async Task<ActionResult<JobInterviewExternalPanelistDto>> AddExternalPanelist(
        Guid interviewId, [FromBody] AddJobInterviewExternalPanelistDto dto)
    {
        dto.JobInterviewId = interviewId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddExternalPanelistAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpDelete("external-panelists/{externalPanelistId:guid}")]
    public async Task<IActionResult> RemoveExternalPanelist(Guid externalPanelistId)
    {
        await _service.RemoveExternalPanelistAsync(externalPanelistId);
        return NoContent();
    }

    [HttpPut("external-panelists/{externalPanelistId:guid}")]
    public async Task<ActionResult<JobInterviewExternalPanelistDto>> UpdateExternalPanelist(
        Guid externalPanelistId, [FromBody] UpdateJobInterviewExternalPanelistDto dto)
    {
        if (externalPanelistId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateExternalPanelistAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // INTERVIEWEES
    // =========================================================================

    [HttpGet("{interviewId:guid}/interviewees")]
    public async Task<ActionResult<IEnumerable<JobIntervieweeDto>>> GetInterviewees(Guid interviewId)
        => Ok(await _service.GetIntervieweesAsync(interviewId));

    [HttpPost("{interviewId:guid}/interviewees")]
    public async Task<ActionResult<JobIntervieweeDto>> AddInterviewee(
        Guid interviewId, [FromBody] AddJobIntervieweeDto dto)
    {
        dto.JobInterviewId = interviewId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddIntervieweeAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpDelete("interviewees/{intervieweeId:guid}")]
    public async Task<IActionResult> RemoveInterviewee(Guid intervieweeId)
    {
        await _service.RemoveIntervieweeAsync(intervieweeId);
        return NoContent();
    }

    [HttpPost("{interviewId:guid}/send-invites")]
    public async Task<ActionResult<SendInterviewInvitesResultDto>> SendInvites(
        Guid interviewId, [FromBody] SendInterviewInvitesDto dto)
    {
        var result = await _service.SendInvitesAsync(interviewId, dto.ApplicationIds);
        return Ok(result);
    }

    /// <summary>
    /// Anonymous endpoint — called when a panelist clicks "Confirm assignment" in their invitation email.
    /// </summary>
    [HttpGet("confirm-panelist/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("PublicPortalPolicy")]
    public async Task<ActionResult<ConfirmPanelistAssignmentResultDto>> ConfirmPanelistAssignment(
        string token, CancellationToken ct)
    {
        try
        {
            var result = await _service.ConfirmPanelistAssignmentByTokenAsync(token, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Anonymous endpoint — called when a candidate clicks "Confirm attendance" in their invitation email.
    /// The token is unique per interviewee and is invalidated (regenerated) when the interview is rescheduled.
    /// </summary>
    [HttpGet("confirm-attendance/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("PublicPortalPolicy")]
    public async Task<ActionResult<ConfirmInterviewAttendanceResultDto>> ConfirmAttendance(
        string token, CancellationToken ct)
    {
        try
        {
            var result = await _service.ConfirmAttendanceByTokenAsync(token, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPatch("interviewees/{intervieweeId:guid}/slot")]
    public async Task<IActionResult> UpdateIntervieweeSlot(
        Guid intervieweeId, [FromBody] UpdateIntervieweeSlotDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.IntervieweeId = intervieweeId;
        await _service.UpdateIntervieweeSlotAsync(dto);
        return Ok(new { message = "Slot time updated." });
    }

    /// <summary>
    /// What the day would look like at this interval — writes nothing.
    /// </summary>
    /// <remarks>
    /// Answers the question the schedule screen exists to ask: <i>can we see all of them today?</i>
    /// The response carries the timetable, whoever does not fit, and the first free time afterwards.
    /// Readable by a panelist as well as HR; only applying it is an HR act.
    /// </remarks>
    [HttpPost("{interviewId:guid}/slots/preview")]
    [ProducesResponseType(typeof(InterviewSlotPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InterviewSlotPlanDto>> PreviewSlots(
        Guid interviewId, [FromBody] ApportionInterviewSlotsDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.InterviewId = interviewId;
        return Ok(await _service.PreviewSlotApportionmentAsync(dto, ct));
    }

    /// <summary>Writes the timetable onto the session's candidates.</summary>
    [HttpPost("{interviewId:guid}/slots/apply")]
    [ProducesResponseType(typeof(InterviewSlotPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InterviewSlotPlanDto>> ApplySlots(
        Guid interviewId, [FromBody] ApportionInterviewSlotsDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.InterviewId = interviewId;
        return Ok(await _service.ApplySlotApportionmentAsync(dto, ct));
    }

    [HttpPost("interviewees/{intervieweeId:guid}/attendance")]
    public async Task<IActionResult> RecordIntervieweeAttendance(
        Guid intervieweeId, [FromBody] RecordIntervieweeAttendanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordAttendanceAsync(intervieweeId, dto.Attended, dto.NoShowReason, employeeId.Value);
        return Ok(new { message = "Attendance recorded." });
    }

    [HttpPost("interviewees/{intervieweeId:guid}/outcome")]
    public async Task<IActionResult> RecordIntervieweeOutcome(
        Guid intervieweeId, [FromBody] RecordIntervieweeOutcomeDto dto)
    {
        dto.IntervieweeId = intervieweeId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordOutcomeAsync(dto, employeeId.Value);
        return Ok(new { message = "Outcome recorded." });
    }

    [HttpGet("application/{applicationId:guid}/interviewees")]
    public async Task<ActionResult<IEnumerable<JobIntervieweeDto>>> GetInterviewsByApplication(Guid applicationId)
        => Ok(await _service.GetInterviewsByApplicationAsync(applicationId));

    // =========================================================================
    // QUESTION PLANS
    // =========================================================================

    [HttpGet("{interviewId:guid}/question-plans")]
    public async Task<ActionResult<IEnumerable<JobInterviewQuestionDto>>> GetQuestionPlans(Guid interviewId)
        => Ok(await _service.GetQuestionPlansAsync(interviewId));

    [HttpPost("{interviewId:guid}/question-plans")]
    public async Task<ActionResult<JobInterviewQuestionDto>> AddQuestionPlan(
        Guid interviewId, [FromBody] CreateJobInterviewQuestionDto dto)
    {
        dto.JobInterviewId = interviewId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddQuestionPlanAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("question-plans/{questionPlanId:guid}")]
    public async Task<ActionResult<JobInterviewQuestionDto>> UpdateQuestionPlan(
        Guid questionPlanId, [FromBody] UpdateJobInterviewQuestionDto dto)
    {
        if (questionPlanId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateQuestionPlanAsync(dto, employeeId.Value));
    }

    [HttpDelete("question-plans/{questionPlanId:guid}")]
    public async Task<IActionResult> DeleteQuestionPlan(Guid questionPlanId)
    {
        await _service.DeleteQuestionPlanAsync(questionPlanId);
        return NoContent();
    }

    [HttpPost("question-plans/{questionPlanId:guid}/selected-questions/{questionDetailId:guid}")]
    public async Task<IActionResult> AddSelectedQuestion(Guid questionPlanId, Guid questionDetailId)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.AddSelectedQuestionAsync(questionPlanId, questionDetailId, employeeId.Value);
        return Ok(new { message = "Question added to plan." });
    }

    [HttpDelete("question-plans/{questionPlanId:guid}/selected-questions/{questionDetailId:guid}")]
    public async Task<IActionResult> RemoveSelectedQuestion(Guid questionPlanId, Guid questionDetailId)
    {
        await _service.RemoveSelectedQuestionAsync(questionPlanId, questionDetailId);
        return NoContent();
    }

    /// <summary>
    /// Re-randomize the locked question set for every plan attached to an interview.
    /// Replaces any existing selections in-place.
    /// </summary>
    [HttpPost("{id:guid}/select-questions")]
    public async Task<IActionResult> SelectQuestions(Guid id)
    {
        await _service.SelectQuestionsAsync(id);
        return Ok(new { message = "Questions selected." });
    }

    /// <summary>
    /// Dry-run randomisation — returns proposed questions for each plan without saving.
    /// </summary>
    [HttpPost("{id:guid}/preview-select-questions")]
    public async Task<IActionResult> PreviewSelectQuestions(Guid id)
    {
        var preview = await _service.PreviewSelectQuestionsAsync(id);
        return Ok(preview);
    }

    /// <summary>
    /// Saves the user-confirmed question selections for each plan of an interview.
    /// </summary>
    [HttpPost("{id:guid}/commit-questions")]
    public async Task<IActionResult> CommitQuestions(Guid id, [FromBody] CommitInterviewQuestionsDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _service.CommitSelectedQuestionsAsync(id, dto);
        return Ok(new { message = "Questions saved." });
    }

    // =========================================================================
    // SCORE SUMMARIES
    // =========================================================================

    [HttpGet("interviewees/{intervieweeId:guid}/score-summaries")]
    public async Task<ActionResult<IEnumerable<JobInterviewScoreSummaryDto>>> GetScoreSummaries(Guid intervieweeId)
        => Ok(await _service.GetScoreSummariesAsync(intervieweeId));

    [HttpPost("interviewees/{intervieweeId:guid}/score-summaries")]
    public async Task<ActionResult<JobInterviewScoreSummaryDto>> CreateScoreSummary(
        Guid intervieweeId, [FromBody] CreateJobInterviewScoreSummaryDto dto)
    {
        dto.JobIntervieweeId = intervieweeId;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.CreateScoreSummaryAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpGet("score-summaries/{scoreSummaryId:guid}")]
    public async Task<ActionResult<JobInterviewScoreSummaryDetailDto>> GetScoreSummaryDetail(Guid scoreSummaryId)
        => Ok(await _service.GetScoreSummaryDetailAsync(scoreSummaryId));

    [HttpGet("score-summaries/{scoreSummaryId:guid}/entries")]
    public async Task<ActionResult<IEnumerable<JobInterviewScoreEntryDto>>> GetScoreEntries(Guid scoreSummaryId)
        => Ok(await _service.GetScoreEntriesAsync(scoreSummaryId));

    [HttpPost("score-summaries/{scoreSummaryId:guid}/finalize")]
    public async Task<IActionResult> FinalizeScore(Guid scoreSummaryId)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.FinalizeScoreAsync(scoreSummaryId, employeeId.Value);
        return Ok(new { message = "Score finalized." });
    }

    [HttpGet("panelists/{panelistId:guid}/scores")]
    public async Task<ActionResult<IEnumerable<JobInterviewScoreSummaryDto>>> GetScoresByInternalPanelist(
        Guid panelistId)
        => Ok(await _service.GetScoresByInternalPanelistAsync(panelistId));

    [HttpGet("external-panelists/{externalPanelistId:guid}/scores")]
    public async Task<ActionResult<IEnumerable<JobInterviewScoreSummaryDto>>> GetScoresByExternalPanelist(
        Guid externalPanelistId)
        => Ok(await _service.GetScoresByExternalPanelistAsync(externalPanelistId));

    [HttpGet("interviewees/{intervieweeId:guid}/finalized-scores")]
    public async Task<ActionResult<IEnumerable<JobInterviewScoreSummaryDto>>> GetFinalizedScores(Guid intervieweeId)
        => Ok(await _service.GetFinalizedScoresForIntervieweeAsync(intervieweeId));

    // ── Score drafts ──────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/score-drafts/{intervieweeId:guid}")]
    public async Task<ActionResult<InterviewScoreDraftDto?>> GetScoreDraft(
        Guid id,
        Guid intervieweeId,
        [FromQuery] Guid? internalPanelistId,
        [FromQuery] Guid? externalPanelistId)
        => Ok(await _service.GetScoreDraftAsync(intervieweeId, internalPanelistId, externalPanelistId));

    [HttpPut("{id:guid}/score-drafts/{intervieweeId:guid}")]
    public async Task<ActionResult<InterviewScoreDraftDto>> SaveScoreDraft(
        Guid id, Guid intervieweeId, [FromBody] SaveInterviewScoreDraftDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var tenantId = _currentUser.TenantId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        dto.JobIntervieweeId = intervieweeId;
        var result = await _service.SaveScoreDraftAsync(id, dto, tenantId.Value);
        return Ok(result);
    }

    [HttpPost("{id:guid}/panelist-notifications")]
    public async Task<ActionResult<SendPanelistNotificationsResultDto>> SendPanelistNotifications(
        Guid id, [FromBody] SendPanelistNotificationsDto dto)
    {
        var result = await _service.SendPanelistNotificationsAsync(
            id, dto.EmployeeIds, dto.ExternalAssociateIds);
        return Ok(result);
    }
}
