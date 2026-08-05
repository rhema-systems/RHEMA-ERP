using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

/// <summary>
/// Finance-owned physical till workspace. Permission policy is applied centrally by the Finance
/// authorization convention; every service query also enforces the current tenant boundary.
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/cashier-tills")]
public sealed class CashierTillController : ControllerBase
{
    private readonly ICashierTillService _service;

    public CashierTillController(ICashierTillService service)
    {
        _service = service;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<CashierTillSessionDto>>> GetSessions(
        [FromQuery] CashierTillSessionStatus? status = null,
        [FromQuery] Guid? liquidityAccountId = null,
        [FromQuery] DateTime? businessDate = null,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetSessionsAsync(status, liquidityAccountId, businessDate, cancellationToken));

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<CashierTillSessionDto>> GetSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetSessionAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<CashierTillSessionDto>> OpenSession(
        [FromBody] OpenCashierTillSessionDto dto,
        CancellationToken cancellationToken)
    {
        var item = await _service.OpenSessionAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetSession), new { id = item.Id }, item);
    }

    [HttpPost("sessions/{id:guid}/submit-count")]
    public async Task<ActionResult<CashierTillSessionDto>> SubmitCount(
        Guid id,
        [FromBody] SubmitCashierTillCountDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.SubmitCountAsync(id, dto, cancellationToken));

    [HttpPost("sessions/{id:guid}/approve-closure")]
    public async Task<ActionResult<CashierTillSessionDto>> ApproveClosure(
        Guid id,
        [FromBody] ReviewCashierTillSessionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ApproveClosureAsync(id, dto, cancellationToken));

    [HttpPost("sessions/{id:guid}/return-for-recount")]
    public async Task<ActionResult<CashierTillSessionDto>> ReturnForRecount(
        Guid id,
        [FromBody] ReviewCashierTillSessionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ReturnForRecountAsync(id, dto, cancellationToken));

    [HttpPost("sessions/{id:guid}/reopen-as-correction")]
    public async Task<ActionResult<CashierTillSessionDto>> ReopenAsCorrection(
        Guid id,
        [FromBody] ReopenCashierTillSessionDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.ReopenAsCorrectionAsync(id, dto, cancellationToken));
}
