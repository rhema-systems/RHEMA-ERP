using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// An employee's own part in the staff awards: what is open, who they may put forward, and the
/// nominations they have raised.
/// </summary>
/// <remarks>
/// <para><b>Why this exists (area 14 slice 4, decision D-1).</b> TDC's <i>Staff Awards Changes</i>
/// note puts employees at the centre of the feature — <i>"employees nominating through staff
/// portal"</i>, <i>"if the employees themselves must choose or nominate people"</i>, and pointedly
/// <i>"nominate managers for awards"</i>. Slice 1 put the whole of <c>AwardsController</c> behind
/// <c>HR.Awards.*</c>, which is right for the awards desk and would lock every one of those
/// employees out of the thing the area exists for. That was the area-15b trap: a permission gate
/// used for an actor defined by the record rather than by a grant.</para>
///
/// <para>"The portal" is read as the employee's authenticated area of this application rather than
/// the external portal, because 5,520 of 5,579 employees already hold a linked login and the module
/// already serves employees this way in staff travel and medical.</para>
///
/// <para><b>The rules, taken from the area-12 self-service surface:</b></para>
/// <list type="number">
/// <item>No route or query parameter carries an employee id. The actor is the token, always.</item>
/// <item>Someone else's nomination is a <b>404, not a 403</b> — a 403 confirms the id exists, which
/// turns this surface into an oracle for enumerating nomination ids.</item>
/// <item>Privileged operations have <b>no route here at all</b>. Scoring, conferring, paying and
/// administering the catalogue are absent by construction rather than by a guard that a later edit
/// could weaken.</item>
/// </list>
///
/// <para>Gated on bare <c>[Authorize]</c> deliberately: holding no awards permission is the normal
/// case for the people this controller serves.</para>
/// </remarks>
[ApiController]
[Route("api/awards/me")]
[Authorize]
public class AwardsMeController : HrControllerBase
{
    private readonly IAwardCycleService _cycleService;
    private readonly IAwardEligibilityService _eligibilityService;
    private readonly IAwardNominationService _nominationService;
    private readonly IAwardVotingService _votingService;
    private readonly IAwardCommitteeReviewService _reviewService;
    private readonly IAwardCommitteeScoringService _scoringService;

    public AwardsMeController(
        IAwardCycleService cycleService,
        IAwardEligibilityService eligibilityService,
        IAwardNominationService nominationService,
        IAwardVotingService votingService,
        IAwardCommitteeReviewService reviewService,
        IAwardCommitteeScoringService scoringService,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _cycleService = cycleService;
        _eligibilityService = eligibilityService;
        _nominationService = nominationService;
        _votingService = votingService;
        _reviewService = reviewService;
        _scoringService = scoringService;
    }

    /// <summary>
    /// The caller's own nomination, or null when it is not theirs or does not exist. Callers turn
    /// null into 404 — never 403, and never a message that distinguishes the two cases.
    /// </summary>
    private async Task<AwardNominationDto?> GetOwnNominationAsync(Guid id, Guid employeeId)
    {
        try
        {
            var nomination = await _nominationService.GetByIdAsync(id);
            return nomination != null && nomination.NominatedById == employeeId ? nomination : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Awards currently accepting nominations. The employee's starting point.</summary>
    [HttpGet("cycles/open")]
    public async Task<ActionResult<IEnumerable<AwardCycleSummaryDto>>> GetOpenCycles()
    {
        if (TryGetWriteContext(out _, out _) is { } error) return error;
        return Ok(await _cycleService.GetOpenForNominationAsync());
    }

    /// <summary>
    /// Who the caller may put forward for an award.
    /// </summary>
    /// <remarks>
    /// Only the qualified list is returned here. The awards desk sees the ineligible names and the
    /// reasons — that is how HR checks its own criteria — but an employee choosing somebody to
    /// nominate has no business reading why a colleague failed a rule.
    /// </remarks>
    [HttpGet("awards/{awardTypeId:guid}/candidates")]
    public async Task<ActionResult<IEnumerable<AwardEligibilityVerdictDto>>> GetCandidates(Guid awardTypeId)
    {
        if (TryGetWriteContext(out _, out _) is { } error) return error;

        var result = await _eligibilityService.EvaluateAsync(awardTypeId, null);
        return Ok(result.Eligible);
    }

    /// <summary>Nominations the caller has raised.</summary>
    [HttpGet("nominations")]
    public async Task<ActionResult<IEnumerable<AwardNominationSummaryDto>>> GetMyNominations()
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
            "Listing your nominations") is { } error) return error;

        return Ok(await _nominationService.GetByNominatedByIdAsync(employeeId));
    }

    [HttpGet("nominations/{id:guid}")]
    public async Task<ActionResult<AwardNominationDto>> GetMyNomination(Guid id)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
            "Reading your nomination") is { } error) return error;

        var nomination = await GetOwnNominationAsync(id, employeeId);
        return nomination == null ? NotFound() : Ok(nomination);
    }

    /// <summary>
    /// Put a colleague forward. The nominator is the token — TDC's note has employees nominating
    /// each other, managers included, and slice 1 proved that letting the caller name the nominator
    /// was forgeable.
    /// </summary>
    [HttpPost("nominations")]
    public async Task<ActionResult<AwardNominationDto>> Nominate([FromBody] CreateAwardNominationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var nominatedById,
            "Raising a nomination") is { } error) return error;

        var created = await _nominationService.CreateAsync(tenantId, nominatedById, userId, dto);
        return CreatedAtAction(nameof(GetMyNomination), new { id = created.Id }, created);
    }

    /// <summary>Revise a nomination that has not been submitted yet.</summary>
    [HttpPut("nominations/{id:guid}")]
    public async Task<ActionResult<AwardNominationDto>> UpdateMyNomination(
        Guid id, [FromBody] UpdateAwardNominationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var employeeId,
            "Editing your nomination") is { } error) return error;

        if (await GetOwnNominationAsync(id, employeeId) == null) return NotFound();
        return Ok(await _nominationService.UpdateAsync(id, userId, dto));
    }

    /// <summary>Send it to the awards desk.</summary>
    [HttpPost("nominations/{id:guid}/submit")]
    public async Task<ActionResult<AwardNominationDto>> SubmitMyNomination(Guid id)
    {
        if (TryGetEmployeeWriteContext(out _, out var userId, out var employeeId,
            "Submitting your nomination") is { } error) return error;

        if (await GetOwnNominationAsync(id, employeeId) == null) return NotFound();
        return Ok(await _nominationService.SubmitAsync(id, userId));
    }

    /// <summary>
    /// Withdraw a nomination the caller raised and has not submitted.
    /// </summary>
    /// <remarks>
    /// Deliberately only a draft. Once a nomination is with the awards desk, taking it back is a
    /// decision about a record other people are now acting on, and it belongs to them.
    /// </remarks>
    [HttpDelete("nominations/{id:guid}")]
    public async Task<IActionResult> WithdrawMyNomination(Guid id)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
            "Withdrawing your nomination") is { } error) return error;

        var nomination = await GetOwnNominationAsync(id, employeeId);
        if (nomination == null) return NotFound();

        await _nominationService.WithdrawOwnAsync(id, employeeId);
        return NoContent();
    }

    // ── voting ────────────────────────────────────────────────────────────────

    /// <summary>Cycles currently accepting votes.</summary>
    [HttpGet("cycles/voting")]
    public async Task<ActionResult<IEnumerable<AwardCycleSummaryDto>>> GetVotingCycles()
    {
        if (TryGetWriteContext(out _, out _) is { } error) return error;
        return Ok(await _cycleService.GetOpenForVotingAsync());
    }

    /// <summary>
    /// The ballot: who is standing, whether the caller may vote, and what they already chose.
    /// </summary>
    /// <remarks>
    /// Carries no vote counts. The tally is withheld until voting closes so that it cannot
    /// influence the vote it reports, and a per-nominee count here would be the same disclosure by
    /// another route.
    /// </remarks>
    [HttpGet("cycles/{cycleId:guid}/ballot")]
    public async Task<ActionResult<AwardBallotDto>> GetBallot(Guid cycleId)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var voterId,
            "Viewing a ballot") is { } error) return error;

        return Ok(await _votingService.GetBallotAsync(cycleId, voterId));
    }

    /// <summary>
    /// Cast or change the caller's single vote in a cycle. The voter is the token, never the payload.
    /// </summary>
    [HttpPost("cycles/{cycleId:guid}/vote")]
    public async Task<ActionResult<AwardVoteDto>> Vote(Guid cycleId, [FromBody] CastAwardVoteDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var voterId,
            "Voting") is { } error) return error;

        return Ok(await _votingService.CastAsync(cycleId, voterId, userId, dto));
    }

    [HttpGet("cycles/{cycleId:guid}/vote")]
    public async Task<ActionResult<AwardVoteDto>> GetMyVote(Guid cycleId)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var voterId,
            "Reading your vote") is { } error) return error;

        var vote = await _votingService.GetMyVoteAsync(cycleId, voterId);
        return vote == null ? NotFound() : Ok(vote);
    }

    /// <summary>Take the caller's vote back while the window is still open.</summary>
    [HttpDelete("cycles/{cycleId:guid}/vote")]
    public async Task<IActionResult> WithdrawMyVote(Guid cycleId)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var voterId,
            "Withdrawing your vote") is { } error) return error;

        await _votingService.WithdrawMyVoteAsync(cycleId, voterId);
        return NoContent();
    }


    // ── committee scoring ─────────────────────────────────────────────────────
    //
    // Scoring lives here rather than on AwardsController because a committee member is defined by
    // the record - membership of the committee a nomination was assigned to - and not by an HR
    // grant. Most committee members are HODs and senior staff who hold no awards permission at all;
    // gating the score on HR.Awards.Write refused them before the membership rule could run.
    //
    // The service enforces membership, so this route is deliberately bare [Authorize]: the gate is
    // the committee, not the permission.

    /// <summary>Nominations waiting for the caller's score.</summary>
    [HttpGet("reviews/pending")]
    public async Task<ActionResult<IEnumerable<AwardNominationSummaryDto>>> GetMyPendingReviews()
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var reviewerId,
            "Listing what you have to score") is { } error) return error;

        return Ok(await _reviewService.GetPendingReviewsAsync(reviewerId));
    }

    /// <summary>Scores the caller has already given.</summary>
    [HttpGet("reviews")]
    public async Task<ActionResult<IEnumerable<AwardCommitteeReviewDto>>> GetMyReviews()
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var reviewerId,
            "Listing your scores") is { } error) return error;

        return Ok(await _reviewService.GetByReviewerIdAsync(reviewerId));
    }

    /// <summary>
    /// Score a nomination. The reviewer is the token, and the service refuses anyone who is not an
    /// active member of the committee that nomination was assigned to.
    /// </summary>
    [HttpPost("nominations/{nominationId:guid}/score")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> ScoreNomination(
        Guid nominationId, [FromBody] SubmitCommitteeReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var reviewerId,
            "Scoring a nomination") is { } error) return error;

        return Ok(await _reviewService.SubmitReviewAsync(nominationId, reviewerId, userId, dto));
    }

    /// <summary>Revise a score the caller gave.</summary>
    [HttpPut("reviews/{id:guid}")]
    public async Task<ActionResult<AwardCommitteeReviewDto>> UpdateMyScore(
        Guid id, [FromBody] UpdateCommitteeReviewDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out var userId, out var reviewerId,
            "Revising your score") is { } error) return error;

        var mine = await _reviewService.GetByIdAsync(id);
        if (mine == null || mine.ReviewerId != reviewerId) return NotFound();

        return Ok(await _reviewService.UpdateReviewAsync(id, userId, dto));
    }


    /// <summary>
    /// What the caller's own committee has scored so far.
    /// </summary>
    /// <remarks>
    /// <para><b>The same trap a third time, so it is worth stating as a rule.</b> The desk's
    /// <c>cycles/{id}/committee-result</c> needs <c>HR.Awards.Read</c>, which committee members do
    /// not hold — so the people doing the scoring could not see the scores. Slices 4, 5 and 6 each
    /// began by gating an act on an HR permission when the actor was defined by the record, and each
    /// time the symptom was a 403 that looks like misconfiguration rather than a design error.</para>
    ///
    /// <para>The rule, stated plainly: <b>if the person entitled to do a thing is identified by a
    /// row rather than by a grant, the route belongs on this controller and the row is the gate.</b></para>
    ///
    /// <para>Membership is checked here rather than in the scoring service, because the service
    /// answers a question about a cycle and has no opinion about who is asking.</para>
    /// </remarks>
    [HttpGet("cycles/{cycleId:guid}/committee-result")]
    public async Task<ActionResult<AwardCommitteeResultDto>> GetMyCommitteeResult(Guid cycleId)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var reviewerId,
            "Viewing your committee's scores") is { } error) return error;

        // Only somebody with something to score in this cycle may read its scores. Anyone else asks
        // the awards desk.
        var owed = await _reviewService.GetPendingReviewsAsync(reviewerId);
        var mine = await _reviewService.GetByReviewerIdAsync(reviewerId);
        var involved = owed.Any() || mine.Any();

        if (!involved)
            return NotFound();

        return Ok(await _scoringService.GetResultAsync(cycleId));
    }

}
