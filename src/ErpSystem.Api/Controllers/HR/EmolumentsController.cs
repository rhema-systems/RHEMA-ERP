using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Position-level and employee-level pay-component assignments, plus the consolidated employee
/// emolument roll-up and the derived leave-encashment per-day rate (Phase 4).
/// </summary>
[ApiController]
[Route("api/hr/emoluments")]
[Authorize(Policy = "InternalOnly")]
public class EmolumentsController : ControllerBase
{
    private readonly IEmolumentService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<EmolumentsController> _logger;

    public EmolumentsController(
        IEmolumentService service,
        ICurrentUserService currentUserService,
        IAuthorizationService authorization,
        ILogger<EmolumentsController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _authorization = authorization;
        _logger = logger;
    }

    /// <summary>Self-or-permission (W3), as on LeavesController — see the remarks there.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    // ─── Position-level assignments ────────────────────────────────────────────

    [HttpGet("positions/{positionId:guid}/components")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<PositionPayComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PositionPayComponentDto>>> GetPositionComponents(Guid positionId)
        => Ok(await _service.GetPositionComponentsAsync(positionId));

    [HttpPost("positions/components")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(PositionPayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PositionPayComponentDto>> AssignPositionComponent([FromBody] CreatePositionPayComponentDto dto)
    {
        try { return Ok(await _service.AssignPositionComponentAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("positions/components/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(PositionPayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionPayComponentDto>> UpdatePositionComponent(
        Guid id, [FromBody] UpdatePositionPayComponentRequest dto)
    {
        try { return Ok(await _service.UpdatePositionComponentAsync(id, dto.Amount, dto.IsActive)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("positions/components/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePositionComponent(Guid id)
    {
        try { await _service.RemovePositionComponentAsync(id); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Employee-level assignments ────────────────────────────────────────────

    // W3: self-or-permission — an employee sees their OWN pay makeup (a payslip shows it
    // anyway); a colleague's is the compensation read tier.
    [HttpGet("employees/{employeeId:guid}/components")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePayComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePayComponentDto>>> GetEmployeeComponents(Guid employeeId)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.CompensationReadPolicy))
            return Forbid();
        return Ok(await _service.GetEmployeeComponentsAsync(employeeId));
    }

    [HttpPost("employees/components")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(EmployeePayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePayComponentDto>> AssignEmployeeComponent([FromBody] CreateEmployeePayComponentDto dto)
    {
        try { return Ok(await _service.AssignEmployeeComponentAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("employees/components/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(EmployeePayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePayComponentDto>> UpdateEmployeeComponent(
        Guid id, [FromBody] UpdateEmployeePayComponentDto dto)
    {
        try { return Ok(await _service.UpdateEmployeeComponentAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("employees/components/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveEmployeeComponent(Guid id)
    {
        try { await _service.RemoveEmployeeComponentAsync(id); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Consolidated / derived ────────────────────────────────────────────────

    // W3: self-or-permission, as the components read above.
    [HttpGet("employees/{employeeId:guid}/summary")]
    [ProducesResponseType(typeof(EmployeeEmolumentSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmolumentSummaryDto>> GetEmployeeSummary(
        Guid employeeId, [FromQuery] DateOnly? asOf = null)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.CompensationReadPolicy))
            return Forbid();

        try { return Ok(await _service.GetEmployeeEmolumentSummaryAsync(employeeId, asOf)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // GET encashment-rate — HR's quote of a day of leave in money — was removed in leave settings
    // audit 2 (L-73): pay is Finance's, so HR quotes the days and Finance the amount.
}

public class UpdatePositionPayComponentRequest
{
    public decimal? Amount { get; set; }
    public bool IsActive { get; set; } = true;
}
