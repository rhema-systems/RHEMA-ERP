using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Stop-work authority (FR-SHE-200) — the area's fourth deliberately-open employee
/// action: ANY authenticated employee may raise a stop-work order and read their own
/// (the whole point of stop-work authority is that it is not gatekept). Non-HR
/// raisers always raise as themselves — the raiser comes from the token, never the
/// body. Routing, resolution, clearance and the register are HR-gated.
/// </summary>
[ApiController]
[SafetyBusinessRules]
[Route("api/safety/stop-work")]
[Authorize(Policy = "InternalOnly")]
public class SheStopWorkController : SheApiControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement could not be relaxed for the open raise + mine actions.
    private readonly ISheStopWorkService _service;

    public SheStopWorkController(ISheStopWorkService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── the open surface ──
    /// <summary>Open to any authenticated employee. Non-HR raisers raise as themselves.</summary>
    [HttpPost]
    public async Task<ActionResult<SheStopWorkOrderDto>> Raise([FromBody] CreateSheStopWorkOrderDto dto)
    {
        // W3 slice 12: the on-behalf arm is the desk tier, not the HR role.
        var isDesk = await HoldsPolicyAsync(HrPermissions.SheWritePolicy);
        var raisedById = isDesk && dto.RaisedById is { } id && id != Guid.Empty ? id : UserId;
        var created = await _service.RaiseAsync(dto, raisedById, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>The caller's own raised orders — open self-service (the slice-6 /mine idiom).</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<SheStopWorkOrderDto>>> GetMine()
        => Ok(await _service.GetMineAsync(UserId));

    // ── HR register + lifecycle ──
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheStopWorkOrderDto>>> GetAll([FromQuery] SheStopWorkStatus? status)
        => Ok(await _service.GetAllAsync(status));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheStopWorkOrderDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("number/{orderNumber}")]
    public async Task<ActionResult<SheStopWorkOrderDto?>> GetByNumber(string orderNumber)
        => Ok(await _service.GetByNumberAsync(orderNumber));

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/route")]
    public async Task<ActionResult<SheStopWorkOrderDto>> Route(Guid id, [FromBody] RouteSheStopWorkOrderDto dto)
    {
        dto.OrderId = id;
        return Ok(await _service.RouteAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<SheStopWorkOrderDto>> Resolve(Guid id, [FromBody] ResolveSheStopWorkOrderDto dto)
    {
        dto.OrderId = id;
        return Ok(await _service.ResolveAsync(dto, UserId));
    }

    /// <summary>Authorises resumption. Refused (422) unless the order is Resolved.</summary>
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/clear")]
    public async Task<ActionResult<SheStopWorkOrderDto>> Clear(Guid id, [FromBody] ClearSheStopWorkOrderDto dto)
    {
        dto.OrderId = id;
        return Ok(await _service.ClearAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SheStopWorkOrderDto>> Cancel(Guid id, [FromBody] CancelSheStopWorkOrderDto dto)
    {
        dto.OrderId = id;
        return Ok(await _service.CancelAsync(dto, UserId, UserId));
    }
}
