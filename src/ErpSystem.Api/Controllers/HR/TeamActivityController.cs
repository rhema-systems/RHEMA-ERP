using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// What a team or committee is chartered to do, what it has undertaken, and who is doing it —
/// terms of reference, objectives and tasks.
/// </summary>
/// <remarks>
/// <para>Round 2, lane F1 (plan § 1.4, § 6.6). These hang off the existing <c>Team</c> record, so
/// they share its route prefix; <c>TeamsController</c> keeps the register and its membership.</para>
///
/// <para><b>⚠ THE GATE ON THIS CONTROLLER IS DELIBERATELY THIN, AND THAT IS NOT AN OVERSIGHT.</b>
/// A team's lead is very often not an HR user, so <c>EmployeeWritePolicy</c> on these routes would
/// lock the one person who most needs them out of their own team's plan. The vertical gate is
/// therefore "any internal user", and the horizontal question — <i>is this caller anything to do
/// with THIS team, and in what capacity?</i> — is answered in
/// <see cref="ITeamActivityService"/> against the record, which is the only place it can be
/// answered. Every method there begins with that check.</para>
///
/// <para>The three capacities the service distinguishes: <b>HR</b> (or an admin role) acts on any
/// team; the team's <b>lead or deputy</b> acts on their own team; an ordinary <b>member</b> may
/// move, tick and attach to a task assigned to them, and read everything else. A non-member gets
/// 403 from the read, not an empty list — an empty list would say "this team has no objectives",
/// which is a different and untrue statement.</para>
/// </remarks>
[ApiController]
[Route("api/hr/teams")]
[Authorize(Policy = "InternalOnly")]
[TeamActivityBusinessRules]
public class TeamActivityController : ControllerBase
{
    private readonly ITeamActivityService _service;

    public TeamActivityController(ITeamActivityService service) => _service = service;

    // ── Terms of reference ────────────────────────────────────────────────────

    /// <summary>Every version, newest first.</summary>
    [HttpGet("{teamId:guid}/terms")]
    [ProducesResponseType(typeof(IEnumerable<TeamTermsOfReferenceListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TeamTermsOfReferenceListDto>>> GetTerms(
        Guid teamId, CancellationToken cancellationToken)
        => Ok(await _service.GetTermsAsync(teamId, cancellationToken));

    [HttpGet("terms/{id:guid}")]
    [ProducesResponseType(typeof(TeamTermsOfReferenceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamTermsOfReferenceDetailDto>> GetTermsById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetTermsByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{teamId:guid}/terms")]
    [ProducesResponseType(typeof(TeamTermsOfReferenceDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamTermsOfReferenceDetailDto>> CreateTerms(
        Guid teamId, [FromBody] CreateTeamTermsOfReferenceDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateTermsAsync(teamId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetTermsById), new { id = created.Id }, created);
    }

    /// <summary>Edits a draft. An approved version is immutable — take a new one instead.</summary>
    [HttpPut("terms/{id:guid}")]
    [ProducesResponseType(typeof(TeamTermsOfReferenceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamTermsOfReferenceDetailDto>> UpdateTerms(
        Guid id, [FromBody] UpdateTeamTermsOfReferenceDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateTermsAsync(id, dto, cancellationToken));
    }

    [HttpPost("terms/{id:guid}/submit")]
    [ProducesResponseType(typeof(TeamTermsOfReferenceDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamTermsOfReferenceDetailDto>> SubmitTerms(
        Guid id, CancellationToken cancellationToken)
        => Ok(await _service.SubmitTermsAsync(id, cancellationToken));

    /// <summary>Approves it, superseding whatever approved version the team held before.</summary>
    [HttpPost("terms/{id:guid}/approve")]
    [ProducesResponseType(typeof(TeamTermsOfReferenceDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamTermsOfReferenceDetailDto>> ApproveTerms(
        Guid id, CancellationToken cancellationToken)
        => Ok(await _service.ApproveTermsAsync(id, cancellationToken));

    /// <summary>Clones the approved terms to a new draft at version + 1.</summary>
    [HttpPost("terms/{id:guid}/new-version")]
    [ProducesResponseType(typeof(TeamTermsOfReferenceDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamTermsOfReferenceDetailDto>> NewTermsVersion(
        Guid id, CancellationToken cancellationToken)
        => Ok(await _service.NewTermsVersionAsync(id, cancellationToken));

    /// <summary>Discards a draft. Approved and superseded versions are part of the record.</summary>
    [HttpDelete("terms/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteTerms(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteTermsAsync(id, cancellationToken);
        return NoContent();
    }

    // ── Objectives ────────────────────────────────────────────────────────────

    [HttpGet("{teamId:guid}/objectives")]
    [ProducesResponseType(typeof(IEnumerable<TeamObjectiveListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamObjectiveListDto>>> GetObjectives(
        Guid teamId, CancellationToken cancellationToken)
        => Ok(await _service.GetObjectivesAsync(teamId, cancellationToken));

    /// <summary>
    /// What the team's active objective weights add up to.
    /// </summary>
    /// <remarks>⚠ Advisory. Nothing refuses a write because this is not 100.</remarks>
    [HttpGet("{teamId:guid}/objectives/weight-total")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetObjectiveWeightTotal(Guid teamId, CancellationToken cancellationToken)
    {
        var (total, isBalanced) = await _service.GetObjectiveWeightTotalAsync(teamId, cancellationToken);
        return Ok(new { total, isBalanced });
    }

    [HttpGet("objectives/{id:guid}")]
    [ProducesResponseType(typeof(TeamObjectiveDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamObjectiveDetailDto>> GetObjectiveById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetObjectiveByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{teamId:guid}/objectives")]
    [ProducesResponseType(typeof(TeamObjectiveDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TeamObjectiveDetailDto>> CreateObjective(
        Guid teamId, [FromBody] CreateTeamObjectiveDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateObjectiveAsync(teamId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetObjectiveById), new { id = created.Id }, created);
    }

    [HttpPut("objectives/{id:guid}")]
    [ProducesResponseType(typeof(TeamObjectiveDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamObjectiveDetailDto>> UpdateObjective(
        Guid id, [FromBody] UpdateTeamObjectiveDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateObjectiveAsync(id, dto, cancellationToken));
    }

    /// <summary>
    /// Moves an objective. Completing below 100 % needs an outcome summary; cancelling needs a reason.
    /// </summary>
    [HttpPost("objectives/{id:guid}/status/{status}")]
    [ProducesResponseType(typeof(TeamObjectiveDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamObjectiveDetailDto>> ChangeObjectiveStatus(
        Guid id, TeamObjectiveStatus status, [FromBody] TeamObjectiveStatusChangeDto? body,
        CancellationToken cancellationToken)
        => Ok(await _service.ChangeObjectiveStatusAsync(
            id, status, body ?? new TeamObjectiveStatusChangeDto(), cancellationToken));

    [HttpDelete("objectives/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteObjective(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteObjectiveAsync(id, cancellationToken);
        return NoContent();
    }

    // ── Tasks ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The team's tasks. <c>mine=true</c> narrows to the caller's own, resolved from the token.
    /// </summary>
    [HttpGet("{teamId:guid}/tasks")]
    [ProducesResponseType(typeof(IEnumerable<TeamTaskListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamTaskListDto>>> GetTasks(
        Guid teamId,
        [FromQuery] Guid? objectiveId = null,
        [FromQuery] bool mine = false,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetTasksAsync(teamId, objectiveId, mine, cancellationToken));

    [HttpGet("tasks/{id:guid}")]
    [ProducesResponseType(typeof(TeamTaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamTaskDetailDto>> GetTaskById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetTaskByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{teamId:guid}/tasks")]
    [ProducesResponseType(typeof(TeamTaskDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TeamTaskDetailDto>> CreateTask(
        Guid teamId, [FromBody] CreateTeamTaskDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateTaskAsync(teamId, dto, cancellationToken);
        return CreatedAtAction(nameof(GetTaskById), new { id = created.Id }, created);
    }

    [HttpPut("tasks/{id:guid}")]
    [ProducesResponseType(typeof(TeamTaskDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamTaskDetailDto>> UpdateTask(
        Guid id, [FromBody] UpdateTeamTaskDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateTaskAsync(id, dto, cancellationToken));
    }

    /// <summary>
    /// Moves a task.
    /// </summary>
    /// <remarks>
    /// ⚠ The one write an ordinary member may make, and only on a task assigned to them — which is
    /// exactly why it is a separate door from the update above. Progressing work you were given is
    /// not the same privilege as rewriting the task record.
    /// </remarks>
    [HttpPost("tasks/{id:guid}/status")]
    [ProducesResponseType(typeof(TeamTaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamTaskDetailDto>> ChangeTaskStatus(
        Guid id, [FromBody] TeamTaskStatusChangeDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.ChangeTaskStatusAsync(id, dto, cancellationToken));
    }

    [HttpDelete("tasks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteTaskAsync(id, cancellationToken);
        return NoContent();
    }

    // ── Checklist ─────────────────────────────────────────────────────────────

    [HttpPost("tasks/{taskId:guid}/checklist")]
    [ProducesResponseType(typeof(TeamTaskChecklistItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamTaskChecklistItemDto>> AddChecklistItem(
        Guid taskId, [FromBody] CreateTeamTaskChecklistItemDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AddChecklistItemAsync(taskId, dto, cancellationToken));
    }

    /// <summary>Renames, reorders or ticks an item. Ticking records who and when, from the token.</summary>
    [HttpPut("checklist/{id:guid}")]
    [ProducesResponseType(typeof(TeamTaskChecklistItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeamTaskChecklistItemDto>> UpdateChecklistItem(
        Guid id, [FromBody] UpdateTeamTaskChecklistItemDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpdateChecklistItemAsync(id, dto, cancellationToken));
    }

    [HttpDelete("checklist/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChecklistItem(Guid id, CancellationToken cancellationToken)
        => await _service.DeleteChecklistItemAsync(id, cancellationToken) ? NoContent() : NotFound();

    // ── Attachments ───────────────────────────────────────────────────────────
    //
    // ⚠ The UPLOAD is not here. A file reaches a task through the controlled gate on
    // TeamActivityDocumentsController, which scans it and stores it in the DMS before this module
    // ever sees an id. A file field on a JSON body is the sink that had to be removed from the
    // guarantor form, the award attachment and three medical documents.

    [HttpGet("tasks/attachments/{id:guid}")]
    [ProducesResponseType(typeof(TeamTaskAttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamTaskAttachmentDto>> GetTaskAttachment(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetTaskAttachmentAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("tasks/attachments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTaskAttachment(Guid id, CancellationToken cancellationToken)
        => await _service.DeleteTaskAttachmentAsync(id, cancellationToken) ? NoContent() : NotFound();
}
