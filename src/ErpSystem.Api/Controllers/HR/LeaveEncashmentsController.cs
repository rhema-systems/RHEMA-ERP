using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Leave encashment processing endpoints
/// </summary>
/// <remarks>
/// W3 slice 5: an employee requests and views their OWN encashment (self-or-permission), the
/// register and the payment step are the leave read/write tiers, and approve/reject stay with
/// the workflow assignee, validated per request by the service.
/// </remarks>
[ApiController]
[Route("api/hr/leave-encashments")]
[Authorize(Policy = "InternalOnly")]
public class LeaveEncashmentsController : ControllerBase
{
    private readonly ILeaveEncashmentService _service;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorization;
    private readonly ILeaveYearContext _leaveYear;
    private readonly ILogger<LeaveEncashmentsController> _logger;

    public LeaveEncashmentsController(
        ILeaveEncashmentService service,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuthorizationService authorization,
        ILeaveYearContext leaveYear,
        ILogger<LeaveEncashmentsController> logger)
    {
        _service = service;
        _db = db;
        _currentUserService = currentUserService;
        _authorization = authorization;
        _leaveYear = leaveYear;
        _logger = logger;
    }

    /// <summary>Self-or-permission, as on LeavesController — see the remarks there.</summary>
    private async Task<bool> CanActForEmployeeAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>Self-or-permission resolved through the encashment's owner.</summary>
    private async Task<bool> CanActOnEncashmentAsync(Guid encashmentId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty &&
            _currentUserService.TenantId is Guid tenantId)
        {
            var mine = await _db.Set<Core.Entities.HR.StaffLeave.LeaveEncashment>()
                .AsNoTracking()
                .AnyAsync(e => e.Id == encashmentId && e.TenantId == tenantId && e.EmployeeId == me);
            if (mine) return true;
        }
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<LeaveEncashmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveEncashmentDto>>> GetAll(
        [FromQuery] int       year        = 0,
        [FromQuery] Guid?     employeeId  = null,
        [FromQuery] Guid?     leaveTypeId = null,
        [FromQuery] DateTime? from        = null,
        [FromQuery] DateTime? to          = null,
        [FromQuery] string?   search      = null)
    {
        if (year == 0) year = await _leaveYear.CurrentYearAsync();
        return Ok(await _service.GetAllEncashmentsAsync(year, employeeId, leaveTypeId, from, to, search));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> GetById(Guid id)
    {
        if (!await CanActOnEncashmentAsync(id, HrPermissions.LeaveReadPolicy))
            return Forbid();

        try { return Ok(await _service.GetByIdAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LeaveEncashmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveEncashmentDto>>> GetByEmployee(
        Guid employeeId,
        [FromQuery] int year = 0)
    {
        if (!await CanActForEmployeeAsync(employeeId, HrPermissions.LeaveReadPolicy))
            return Forbid();

        if (year == 0) year = await _leaveYear.CurrentYearAsync();
        return Ok(await _service.GetEmployeeEncashmentsAsync(employeeId, year));
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> Request([FromBody] CreateLeaveEncashmentDto dto)
    {
        // W3: an employee encashes their OWN leave; raising one for someone else is the HR desk.
        if (!await CanActForEmployeeAsync(dto.EmployeeId, HrPermissions.LeaveWritePolicy))
            return Forbid();

        try
        {
            var result = await _service.RequestEncashmentAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting leave encashment");
            return StatusCode(500, "An error occurred while requesting the encashment");
        }
    }

    // W3: approve and reject are deliberately NOT permission-gated — they are the workflow
    // assignee's acts, validated per request by the service (CanUserApproveAsync).
    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> Approve(Guid id)
    {
        try { return Ok(await _service.ApproveEncashmentAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> Reject(Guid id, [FromBody] string reason)
    {
        try { return Ok(await _service.RejectEncashmentAsync(id, reason)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/process")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveEncashmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveEncashmentDto>> MarkAsProcessed(Guid id, [FromBody] ProcessLeaveEncashmentDto dto)
    {
        try { return Ok(await _service.MarkAsProcessedAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking encashment {id} as processed", id);
            return StatusCode(500, "An error occurred while processing the encashment");
        }
    }
}
