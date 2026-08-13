using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Orientation notifications. The <c>mine</c> endpoints are every user's own inbox; anything that
/// names another recipient, or sends, is HR.
/// </summary>
[ApiController]
[OrientationBusinessRules]
[Route("api/orientation-notifications")]
[Authorize]
public class OrientationNotificationsController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IOrientationNotificationService _service;
    private readonly ICurrentUserService _currentUser;

    public OrientationNotificationsController(IOrientationNotificationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Anyone else's inbox — HR only. Your own is <c>mine</c>.</summary>
    [HttpGet("recipient/{recipientEmployeeId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<OrientationNotificationDto>>> GetByRecipient(
        Guid recipientEmployeeId, [FromQuery] bool unreadOnly = false)
        => Ok(await _service.GetByRecipientAsync(recipientEmployeeId, unreadOnly));

    /// <summary>Notifications for the signed-in employee.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<OrientationNotificationDto>>> GetMine([FromQuery] bool unreadOnly = false)
    {
        if (_currentUser.EmployeeId is not { } employeeId)
            return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.GetByRecipientAsync(employeeId, unreadOnly));
    }

    [HttpGet("mine/unread-count")]
    public async Task<ActionResult<int>> GetMyUnreadCount()
    {
        if (_currentUser.EmployeeId is not { } employeeId)
            return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.GetUnreadCountAsync(employeeId));
    }

    [HttpGet("enrollment/{enrollmentId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<OrientationNotificationDto>>> GetByEnrollment(Guid enrollmentId)
        => Ok(await _service.GetByEnrollmentIdAsync(enrollmentId));

    /// <summary>Sending a notification to someone is an administrative act.</summary>
    [HttpPost]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<OrientationNotificationDto>> Create([FromBody] CreateOrientationNotificationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.CreateAsync(dto, tenantId.Value, employeeId.Value));
    }

    /// <summary>
    /// Marks one of your own notifications read. The service refuses an id belonging to someone else
    /// — read state is per-recipient, and letting anyone clear anyone's inbox would hide a compliance
    /// reminder from the person who owes it.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        if (_currentUser.EmployeeId is not { } employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        await _service.MarkAsReadAsync(id, employeeId);
        return Ok(new { message = "Notification marked as read." });
    }

    [HttpPost("mine/read-all")]
    public async Task<ActionResult<int>> MarkAllAsRead()
    {
        if (_currentUser.EmployeeId is not { } employeeId)
            return BadRequest("Your user account is not linked to an employee record.");
        var count = await _service.MarkAllAsReadAsync(employeeId);
        return Ok(new { message = $"{count} notification(s) marked as read.", count });
    }
}
