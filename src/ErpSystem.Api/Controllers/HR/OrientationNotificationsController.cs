using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/orientation-notifications")]
[Authorize]
public class OrientationNotificationsController : ControllerBase
{
    private readonly IOrientationNotificationService _service;
    private readonly ICurrentUserService _currentUser;

    public OrientationNotificationsController(IOrientationNotificationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("recipient/{recipientEmployeeId:guid}")]
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
    public async Task<ActionResult<IEnumerable<OrientationNotificationDto>>> GetByEnrollment(Guid enrollmentId)
        => Ok(await _service.GetByEnrollmentIdAsync(enrollmentId));

    [HttpPost]
    public async Task<ActionResult<OrientationNotificationDto>> Create([FromBody] CreateOrientationNotificationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.CreateAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _service.MarkAsReadAsync(id);
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
