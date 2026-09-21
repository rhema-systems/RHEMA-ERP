using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Medical boards — convening a panel to rule on an employee's fitness, and recording what it
/// decided (residue plan G4 / R-15b).
/// </summary>
/// <remarks>
/// <para>⚠ <b>Gated on <c>HR.Medical.*</c> throughout, including the reads.</b> A board's findings
/// describe somebody's health, and the SHE↔Medical boundary already settled that anything carrying
/// examination results, restrictions or clearance data takes the Medical policies rather than a role
/// gate.</para>
///
/// <para>⚠ <b>That does NOT mean the HR desk is shut out, and it would be wrong to read it that
/// way.</b> <c>HrStaffGrants</c> gives the HR role <c>ViewMedicalRecords</c> and
/// <c>MaintainMedicalRecords</c>, so HR can read and record boards — deliberately, because HR
/// administers the process even though clinicians decide it. What the Medical policies buy is that
/// entitlement is stated in one grant map rather than implied by a role check, so narrowing it later
/// is an edit to that map and nothing else. An ordinary employee holds neither grant and is refused.
/// Proved in both directions by hr-leave slice 8 [5].</para>
///
/// <para>⚠ <b>There is no endpoint here for acting on a board.</b> Leave reads one to satisfy its
/// evidence rule; separation reads one to justify a medical retirement. Both hold a bare
/// <c>MedicalBoardId</c> and neither writes back. A board records what a panel decided; what anybody
/// does about it belongs to the module that acts.</para>
/// </remarks>
[ApiController]
[Route("api/hr/medical-boards")]
[Authorize(Policy = "InternalOnly")]
public class MedicalBoardsController : ControllerBase
{
    private readonly IMedicalBoardService _service;
    private readonly ILogger<MedicalBoardsController> _logger;

    public MedicalBoardsController(IMedicalBoardService service, ILogger<MedicalBoardsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>The board register, filtered and paged</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.MedicalReadPolicy)]
    [ProducesResponseType(typeof(PagedResult<MedicalBoardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MedicalBoardDto>>> GetBoards(
        [FromQuery] Guid? employeeId,
        [FromQuery] MedicalBoardStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var filter = new MedicalBoardFilterDto
        {
            EmployeeId = employeeId, Status = status, From = from, To = to, Search = search
        };
        return Ok(await _service.GetBoardsAsync(filter, pageNumber, pageSize, ct));
    }

    /// <summary>One board, with its members and sittings</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.MedicalReadPolicy)]
    [ProducesResponseType(typeof(MedicalBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicalBoardDto>> GetBoard(Guid id, CancellationToken ct = default)
    {
        var board = await _service.GetBoardAsync(id, ct);
        return board is null ? NotFound(new { message = $"Medical board '{id}' not found." }) : Ok(board);
    }

    /// <summary>Ask for a board</summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [ProducesResponseType(typeof(MedicalBoardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MedicalBoardDto>> RequestBoard(
        [FromBody] RequestMedicalBoardDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () =>
        {
            var board = await _service.RequestBoardAsync(dto, ct);
            return CreatedAtAction(nameof(GetBoard), new { id = board.Id }, board);
        });

    /// <summary>Appoint a member</summary>
    /// <remarks>⚠ Refused once the board has reported — its membership is part of what its finding means.</remarks>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardMemberDto>> AddMember(
        Guid id, [FromBody] AddMedicalBoardMemberDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardMemberDto>(async () => Ok(await _service.AddMemberAsync(id, dto, ct)));

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<object>> RemoveMember(Guid id, Guid memberId, CancellationToken ct = default)
        => await Guarded<object>(async () =>
        {
            var removed = await _service.RemoveMemberAsync(id, memberId, ct);
            if (!removed) return NotFound(new { message = "Member not found on this board." });
            return Ok(new { removed = true });
        });

    /// <summary>Convene the board</summary>
    /// <remarks>⚠ Refused without members: a board is its panel.</remarks>
    [HttpPut("{id:guid}/convene")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardDto>> Convene(Guid id, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () => Ok(await _service.ConveneAsync(id, ct)));

    /// <summary>Record a sitting</summary>
    [HttpPost("{id:guid}/sittings")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardSittingDto>> RecordSitting(
        Guid id, [FromBody] RecordMedicalBoardSittingDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardSittingDto>(async () => Ok(await _service.RecordSittingAsync(id, dto, ct)));

    /// <summary>The board reports</summary>
    /// <remarks>
    /// ⚠ The only status leave and separation act on, and it cannot be undone. A finding that needs
    /// revisiting is a new board.
    /// </remarks>
    [HttpPut("{id:guid}/conclude")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardDto>> Conclude(
        Guid id, [FromBody] ConcludeMedicalBoardDto dto, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () => Ok(await _service.ConcludeAsync(id, dto, ct)));

    /// <summary>Cancel a board that has not reported</summary>
    [HttpPut("{id:guid}/cancel")]
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    public async Task<ActionResult<MedicalBoardDto>> Cancel(
        Guid id, [FromBody] string reason, CancellationToken ct = default)
        => await Guarded<MedicalBoardDto>(async () => Ok(await _service.CancelAsync(id, reason, ct)));

    /// <summary>
    /// One place for the four outcomes every write here shares, so a new endpoint cannot quietly
    /// turn a refusal into a 500.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Exactly one overload, and every call site names its <c>T</c>.</b> An earlier version had
    /// a second, non-generic <c>Guarded(Func&lt;Task&lt;IActionResult&gt;&gt;)</c> beside it. Because
    /// <c>Ok(...)</c> returns a type convertible to both, overload resolution chose the non-generic
    /// one for every lambda and then could not convert the result back — six compile errors from one
    /// convenience. The explicit type argument is what keeps it unambiguous.
    /// </remarks>
    private async Task<ActionResult<T>> Guarded<T>(Func<Task<ActionResult<T>>> run)
    {
        try { return await run(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Medical board operation failed");
            return StatusCode(500, "An error occurred on the medical board.");
        }
    }
}
