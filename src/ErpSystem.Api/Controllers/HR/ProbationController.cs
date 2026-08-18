using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Probation periods, probation reviews and the confirmation outcome (FRD §A1.4 — FR-HR-031,
/// FR-HR-032, FR-HR-140).
/// </summary>
/// <remarks>
/// <para>Gated on the <c>HR.Probation.*</c> family. Reads and record-keeping are HR's; the three
/// outcome decisions — confirm, extend, terminate — plus deletion require Admin, because they
/// decide whether an employee's appointment becomes permanent. See <c>HrPermissions</c> for why,
/// and for the note that slice 8 moves the real check onto the workflow engine's named confirming
/// authority.</para>
///
/// <para>⚠ <b>The gates are per-action, not on the class, and that is deliberate.</b> Stacked
/// <c>[Authorize]</c> attributes are ANDed, so a class-level policy plus a method-level
/// <c>[Authorize]</c> does not open that method up — the class requirement still applies. Five
/// actions here must be reachable by an ordinary employee (see below), so the only way to exempt
/// them is to carry the policy on every <i>other</i> action instead. Adding a new endpoint means
/// adding its gate; there is no class-level default to fall back on.</para>
///
/// <para><b>The employee-facing surface, and its limits.</b> Exactly five actions take a plain
/// <c>[Authorize]</c> and resolve entitlement from the record inside the service:</para>
/// <list type="bullet">
///   <item><c>GET reviews/mine</c> — the reviews of the caller's own probation. No id parameter,
///   so there is nothing to point at someone else.</item>
///   <item><c>POST reviews/{id}/acknowledge</c> — only the employee the review is about.
///   Acknowledgement is testimony, not administration: HR cannot sign "I have seen this" on
///   somebody's behalf, and neither can the reviewer.</item>
///   <item><c>GET reviews/reviewer/{id}</c> — your own reviewer queue, or anyone's if you are HR.</item>
///   <item><c>POST reviews/{id}/submit</c> — recording the assessment. Only the named reviewer or
///   the second reviewer, checked against the record inside the service.</item>
///   <item><c>POST reviews/{id}/complete</c> — closing the review. The reviewer, or HR on their
///   behalf.</item>
/// </list>
///
/// <para>⚠ <b>Why those last three are not on <c>HR.Probation.Write</c>, which is where they
/// started.</b> A probation review is conducted by the employee's line manager — FR-HR-032 routes
/// the month-5 form to the head — and a line manager holds no HR permission whatsoever. Gating
/// them on Write admitted only HR, whom the service then refuses because HR schedules reviews and
/// does not conduct them, so between the two rules the action was reachable by <i>nobody</i>. The
/// harness caught it on the first run. Where the actor is defined by the record rather than by a
/// job title, entitlement has to be read off the record.</para>
///
/// <para>Everything else an employee might want to see about their own probation still belongs to
/// the employee self-service portal (area 25), not here.</para>
/// </remarks>
[ApiController]
[Route("api/probations")]
public class ProbationController : ControllerBase
{
    private readonly IProbationService _service;
    private readonly ICurrentUserService _currentUser;

    public ProbationController(IProbationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>The acting employee, or a 400 explaining that the account is not employee-linked.</summary>
    private Guid RequireEmployeeId()
        => _currentUser.EmployeeId
           ?? throw new UnauthorizedAccessException(
               "Your user account is not linked to an employee record. Please contact your administrator.");

    private bool IsHrActor()
        => User.IsInRole(Constants.Roles.SuperAdmin)
           || User.IsInRole(Constants.Roles.TenantAdmin)
           || User.IsInRole(Constants.Roles.Hr)
           || User.IsInRole(Constants.Roles.LegacyHrUser);

    // =========================================================================
    // PROBATION QUERIES
    // =========================================================================

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProbationPeriodDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    /// <summary>One page of the probation register: active first, then by end date.</summary>
    /// <remarks>
    /// The area had no list endpoint at all until slice 1 — a register screen had to be assembled
    /// from <c>/active</c> and <c>/status/{status}</c>, neither of which pages or searches.
    /// </remarks>
    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProbationPeriodSummaryDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] ProbationStatus? status = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? search = null)
        => Ok(await _service.GetPagedAsync(page, pageSize, status, employeeId, search));

    /// <summary>
    /// An employee's whole probation history, newest first, with the active one at the top.
    /// </summary>
    /// <remarks>
    /// ⚠ This returns an <b>array</b>. Until slice 1 the service behind it returned a single
    /// object while this signature promised a collection, so a client that mapped over the
    /// response threw — a TypeScript type written from this endpoint's name would have compiled
    /// and been wrong.
    /// </remarks>
    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("{id:guid}/with-reviews")]
    public async Task<ActionResult<ProbationPeriodDetailDto>> GetWithReviews(Guid id)
        => Ok(await _service.GetWithReviewsAsync(id));

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetByStatus(ProbationStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveProbationsAsync());

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("ending-within")]
    public async Task<ActionResult<IEnumerable<ProbationPeriodSummaryDto>>> GetEndingWithin(
        [FromQuery] int daysAhead = 30)
        => Ok(await _service.GetEndingWithinAsync(daysAhead));

    /// <summary>
    /// The probation length that applies to this employee, and where it came from (FR-HR-031).
    /// </summary>
    /// <remarks>
    /// A create form should call this before it renders: the length is a property of the
    /// employee's staff category — senior 6 months, junior 3 — not something a user should be
    /// inventing. It also carries the expiry lead days FR-HR-140's alerts use.
    /// </remarks>
    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("policy/{employeeId:guid}")]
    public async Task<ActionResult<ProbationPolicyDto>> GetPolicyForEmployee(Guid employeeId)
        => Ok(await _service.GetPolicyForEmployeeAsync(employeeId));

    // =========================================================================
    // PROBATION CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.ProbationWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<ProbationPeriodDto>> Create([FromBody] CreateProbationPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        var created = await _service.CreateAsync(dto, tenantId.Value, RequireEmployeeId());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.ProbationAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // PROBATION WORKFLOW
    // =========================================================================

    /// <summary>Extends the probation period and records the extension.</summary>
    /// <remarks>
    /// ⚠ The response is the <b>audit row</b>, not a message. This endpoint used to call a method
    /// that moved the end date, wrote no audit row, and overwrote the probation's outcome notes
    /// with the extension reason — so the extension history was empty by construction and
    /// <c>ExtensionCount</c> had nothing to agree with.
    /// </remarks>
    [Authorize(Policy = HrPermissions.ProbationAdminPolicy)]
    [HttpPost("{id:guid}/extend")]
    public async Task<ActionResult<ProbationExtensionDto>> Extend(
        Guid id, [FromBody] CreateProbationExtensionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.ExtendAsync(id, dto, RequireEmployeeId()));
    }

    /// <summary>Every extension applied to this probation period, newest first.</summary>
    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("{id:guid}/extensions")]
    public async Task<ActionResult<IEnumerable<ProbationExtensionDto>>> GetExtensions(Guid id)
        => Ok(await _service.GetExtensionsAsync(id));

    [Authorize(Policy = HrPermissions.ProbationAdminPolicy)]
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        await _service.ConfirmAsync(id, RequireEmployeeId());
        return Ok(new { message = "Probation confirmed." });
    }

    [Authorize(Policy = HrPermissions.ProbationAdminPolicy)]
    [HttpPost("{id:guid}/terminate")]
    public async Task<IActionResult> Terminate(Guid id, [FromBody] TerminateProbationPeriodDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await _service.TerminateAsync(dto, RequireEmployeeId());
        return Ok(new { message = "Probation terminated." });
    }

    // =========================================================================
    // REVIEWS
    // =========================================================================

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("{probationId:guid}/reviews")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetReviews(Guid probationId)
        => Ok(await _service.GetReviewsAsync(probationId));

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("reviews/status/{status}")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetReviewsByStatus(ProbationReviewStatus status)
        => Ok(await _service.GetReviewsByStatusAsync(status));

    [Authorize(Policy = HrPermissions.ProbationReadPolicy)]
    [HttpGet("reviews/overdue")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetOverdueReviews()
        => Ok(await _service.GetOverdueReviewsAsync());

    /// <summary>A reviewer's own queue — the reviews they must conduct, as reviewer or second reviewer.</summary>
    /// <remarks>
    /// Plain <c>[Authorize]</c>: probation reviews are conducted by line managers, so an HR-only
    /// gate would have hidden this queue from everyone who uses it. You may read your own; HR may
    /// read anyone's.
    /// </remarks>
    [Authorize]
    [HttpGet("reviews/reviewer/{reviewerEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetReviewsByReviewer(Guid reviewerEmployeeId)
    {
        if (!IsHrActor() && RequireEmployeeId() != reviewerEmployeeId)
            return Forbid();

        return Ok(await _service.GetReviewsByReviewerAsync(reviewerEmployeeId));
    }

    /// <summary>The reviews of the caller's own probation.</summary>
    /// <remarks>Token-derived: there is no id here to point at someone else's record.</remarks>
    [Authorize]
    [HttpGet("reviews/mine")]
    public async Task<ActionResult<IEnumerable<ProbationReviewDto>>> GetMyReviews()
        => Ok(await _service.GetMyReviewsAsync(RequireEmployeeId()));

    [Authorize(Policy = HrPermissions.ProbationWritePolicy)]
    [HttpPost("{probationId:guid}/reviews")]
    public async Task<ActionResult<ProbationReviewDto>> AddReview(
        Guid probationId, [FromBody] CreateProbationReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");

        return Ok(await _service.AddReviewAsync(dto, tenantId.Value, RequireEmployeeId()));
    }

    /// <summary>Reschedules a review, or names a second reviewer. Not the assessment — see submit.</summary>
    [Authorize(Policy = HrPermissions.ProbationWritePolicy)]
    [HttpPut("reviews/{reviewId:guid}")]
    public async Task<ActionResult<ProbationReviewDto>> UpdateReview(
        Guid reviewId, [FromBody] UpdateProbationReviewDto dto)
    {
        if (reviewId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        return Ok(await _service.UpdateReviewAsync(dto, RequireEmployeeId()));
    }

    /// <summary>
    /// The reviewer records what they found: ratings, strengths, areas for improvement, comments
    /// and a recommendation.
    /// </summary>
    /// <remarks>
    /// ⚠ Before slice 2 none of those fields had a writer anywhere in the solution, so a review
    /// could only be scheduled and then completed while empty. Only the named reviewer or the
    /// second reviewer may submit; HR schedules reviews, it does not conduct them.
    /// </remarks>
    [Authorize]
    [HttpPost("reviews/{reviewId:guid}/submit")]
    public async Task<ActionResult<ProbationReviewDto>> SubmitReview(
        Guid reviewId, [FromBody] SubmitProbationReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.SubmitReviewAsync(reviewId, dto, RequireEmployeeId()));
    }

    /// <summary>The employee records that they have seen the review, and may respond to it.</summary>
    /// <remarks>
    /// Plain <c>[Authorize]</c>, and the service admits <b>only the employee the review is
    /// about</b> — not HR, not the reviewer. The acknowledgement date is stamped server-side.
    /// </remarks>
    [Authorize]
    [HttpPost("reviews/{reviewId:guid}/acknowledge")]
    public async Task<ActionResult<ProbationReviewDto>> AcknowledgeReview(
        Guid reviewId, [FromBody] AcknowledgeProbationReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AcknowledgeReviewAsync(reviewId, dto, RequireEmployeeId()));
    }

    /// <summary>HR sign-off on a completed review. The approver comes from the token.</summary>
    [Authorize(Policy = HrPermissions.ProbationWritePolicy)]
    [HttpPost("reviews/{reviewId:guid}/hr-approve")]
    public async Task<ActionResult<ProbationReviewDto>> HrApproveReview(
        Guid reviewId, [FromBody] ApproveProbationReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.HrApproveReviewAsync(reviewId, dto, RequireEmployeeId()));
    }

    /// <summary>Marks a review conducted. Refuses a review that carries no assessment.</summary>
    /// <remarks>The reviewer, or HR on their behalf — see the note on the class.</remarks>
    [Authorize]
    [HttpPost("reviews/{reviewId:guid}/complete")]
    public async Task<IActionResult> CompleteReview(Guid reviewId)
    {
        await _service.CompleteReviewAsync(reviewId, RequireEmployeeId());
        return Ok(new { message = "Review completed." });
    }
}
