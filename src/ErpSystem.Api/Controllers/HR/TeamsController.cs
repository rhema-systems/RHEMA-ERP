using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The teams register — working groups and who is in them.
/// </summary>
/// <remarks>
/// <para>
/// Built in slice 4b. Distinct from <c>OrganizationUnitController</c>: a unit is the formal
/// hierarchy a person is posted into, a team is who they actually work with. In a matrix
/// organisation those differ, which is why membership carries an allocation percentage and a
/// primary-team flag.
/// </para>
/// <para>
/// Gated like the rest of the org backbone: reads are open to any authenticated user, because a
/// team roster is the same kind of common knowledge as an org chart. Writes are HR's and the
/// administrators' — a team defines reporting and allocation, so it is not self-service.
/// </para>
/// <para>
/// Every action maps <c>InvalidOperationException</c> to 400 and <c>ArgumentException</c> to 404,
/// deliberately and everywhere. Slice 3's D-18 was 42 business rules on a sibling controller all
/// arriving as <c>500 "An error occurred while…"</c>, which is a rule that fires correctly and
/// cannot explain itself — the user is left staring at a refusal with nothing to act on.
/// </para>
/// </remarks>
[ApiController]
[Route("api/hr/teams")]
[Authorize(Policy = "InternalOnly")]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _service;
    private readonly ILogger<TeamsController> _logger;

    public TeamsController(ITeamService service, ILogger<TeamsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ── teams ─────────────────────────────────────────────────────────────────

    /// <summary>All teams. Pass <c>includeInactive=true</c> to see dissolved and draft ones too.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TeamDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetAllAsync(includeInactive, ct)), "retrieving teams");

    /// <summary>Lightweight shape for pickers and parent-team selectors.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<TeamSummaryDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetSummary(CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetAllSummaryAsync(ct)), "retrieving the team summary");

    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<TeamDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetPagedAsync(pageNumber, pageSize, search, ct)), "paging teams");

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetByIdAsync(id, ct)), "retrieving the team");

    /// <summary>The team with its full roster, current members and former ones alike.</summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(TeamDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetDetail(Guid id, CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetDetailByIdAsync(id, ct)), "retrieving the team detail");

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Create([FromBody] CreateTeamDto dto, CancellationToken ct = default)
        => Guard(async () =>
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }, "creating the team");

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamDto dto, CancellationToken ct = default)
        => Guard(async () =>
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (id != dto.Id) return BadRequest(new { message = "The route id and the body id do not match." });
            return Ok(await _service.UpdateAsync(dto, ct));
        }, "updating the team");

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        => Guard(async () => Ok(new { deleted = await _service.DeleteAsync(id, ct) }), "deleting the team");

    // ── membership ────────────────────────────────────────────────────────────

    /// <summary>The roster. Pass <c>currentOnly=true</c> for the people on the team right now.</summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IEnumerable<TeamMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetMembers(Guid id, [FromQuery] bool currentOnly = false, CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetMembersAsync(id, currentOnly, ct)), "retrieving the roster");

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(TeamMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> AddMember(Guid id, [FromBody] AddTeamMemberDto dto, CancellationToken ct = default)
        => Guard(async () =>
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _service.AddMemberAsync(id, dto, ct));
        }, "adding the team member");

    [HttpPut("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(TeamMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateMember(
        Guid id, Guid memberId, [FromBody] UpdateTeamMemberDto dto, CancellationToken ct = default)
        => Guard(async () =>
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Ok(await _service.UpdateMemberAsync(id, memberId, dto, ct));
        }, "updating the team member");

    /// <summary>
    /// Ends a membership. The row stays, carrying its leaving date — a team's record of who was on
    /// it is part of what the register is for.
    /// </summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RemoveMember(
        Guid id, Guid memberId, [FromBody] RemoveTeamMemberDto? dto, CancellationToken ct = default)
        => Guard(async () => Ok(new
        {
            removed = await _service.RemoveMemberAsync(id, memberId, dto ?? new RemoveTeamMemberDto(), ct),
        }), "removing the team member");

    /// <summary>Role changes on this team, newest first.</summary>
    [HttpGet("{id:guid}/member-history")]
    [ProducesResponseType(typeof(IEnumerable<TeamMemberHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetMemberHistory(Guid id, CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetMemberHistoryAsync(id, ct)), "retrieving the membership history");

    /// <summary>Every team an employee belongs to — the read an employee profile needs.</summary>
    [HttpGet("~/api/hr/employees/{employeeId:guid}/teams")]
    [ProducesResponseType(typeof(IEnumerable<TeamMemberDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetForEmployee(
        Guid employeeId, [FromQuery] bool currentOnly = true, CancellationToken ct = default)
        => Guard(async () => Ok(await _service.GetMembershipsForEmployeeAsync(employeeId, currentOnly, ct)),
            "retrieving the employee's teams");

    /// <summary>
    /// One place where every action's exceptions become the right status code and keep their
    /// sentence. Repeating a try/catch per action is how one of them ends up missing a case.
    /// </summary>
    private async Task<IActionResult> Guard(Func<Task<IActionResult>> action, string what)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault: the caller can act on this sentence.
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            // NOT `Forbid(ex.Message)` — that overload takes authentication SCHEME names, so the
            // message would be looked up as a scheme and throw. StatusCode keeps the sentence.
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {What}", what);
            return StatusCode(500, $"An error occurred while {what}.");
        }
    }
}
