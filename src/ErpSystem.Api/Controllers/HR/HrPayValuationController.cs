using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Finance's step: the pay HR records in days, valued (leave settings audit 2, decision P2).
/// </summary>
/// <remarks>
/// <para>The stakeholders asked for HR to leave the monetary aspects and the calculation to Finance
/// and submit the details, such as the days encashed. HR records the days on a leaver's settlement
/// (notice pay in lieu, annual leave owed) and on leave cashed in while employed; the holder of
/// <c>HR.Pay.Value</c> — Finance — puts the money on them here. HR cannot price a pay line, and the
/// statement cannot be finalised while one awaits a figure.</para>
///
/// <para>⚠ Finance sees only what it values: the queue and the statements in it. It holds no read of
/// HR's records at large — the separation's reasons, exit interview and correspondence stay HR's.
/// Leave cashed in is paid through <c>PATCH api/hr/leave-encashments/{id}/process</c>, gated on the
/// same permission.</para>
/// </remarks>
[ApiController]
[Route("api/hr/pay-valuation")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Policy = HrPermissions.PayValuePolicy)]
public class HrPayValuationController : ControllerBase
{
    private readonly ISeparationService _separations;
    private readonly ILeaveEncashmentService _encashments;
    private readonly ICurrentUserService _currentUser;

    public HrPayValuationController(
        ISeparationService separations,
        ILeaveEncashmentService encashments,
        ICurrentUserService currentUser)
    {
        _separations = separations;
        _encashments = encashments;
        _currentUser = currentUser;
    }

    /// <summary>What awaits Finance: statements with pay lines to value, then leave cashed in to pay.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PayToValueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PayToValueItemDto>>> GetQueue(CancellationToken ct)
    {
        var settlements = await _separations.GetSettlementsAwaitingValuationAsync(ct);
        var encashments = await _encashments.GetAwaitingPaymentAsync();
        return Ok(settlements.Concat(encashments).ToList());
    }

    /// <summary>A leaver's statement, whole — the pay lines to value and everything they sit beside.</summary>
    [HttpGet("settlements/{separationId:guid}")]
    [ProducesResponseType(typeof(SeparationSettlementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementDto>> GetSettlement(Guid separationId, CancellationToken ct)
    {
        try { return Ok(await _separations.GetSettlementAsync(separationId, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Value one pay line: the amount, and where it came from.</summary>
    [HttpPut("settlement-lines/{lineId:guid}")]
    [ProducesResponseType(typeof(SeparationSettlementLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeparationSettlementLineDto>> ValueLine(
        Guid lineId, [FromBody] ValueSettlementLineDto dto, CancellationToken ct)
    {
        try { return Ok(await _separations.ValueSettlementLineAsync(lineId, dto, _currentUser.EmployeeId, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
