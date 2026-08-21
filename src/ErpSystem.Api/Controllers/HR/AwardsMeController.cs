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

    public AwardsMeController(
        IAwardCycleService cycleService,
        IAwardEligibilityService eligibilityService,
        IAwardNominationService nominationService,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _cycleService = cycleService;
        _eligibilityService = eligibilityService;
        _nominationService = nominationService;
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
}
