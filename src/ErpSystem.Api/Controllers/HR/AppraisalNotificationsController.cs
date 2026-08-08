using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalNotificationsController : ControllerBase
{
    private readonly IAppraisalNotificationService _notificationService;
    private readonly ILogger<AppraisalNotificationsController> _logger;

    public AppraisalNotificationsController(
        IAppraisalNotificationService notificationService,
        ILogger<AppraisalNotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger              = logger;
    }

    /// <summary>
    /// Get the notification bell summary (unread count + most-recent notifications).
    /// </summary>
    [HttpGet("summary/{employeeId:guid}")]
    public async Task<IActionResult> GetSummary(Guid employeeId, [FromQuery] int recentCount = 20,
        CancellationToken ct = default)
    {
        var result = await _notificationService.GetNotificationSummaryAsync(employeeId, recentCount, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get paginated notifications for an employee.
    /// </summary>
    [HttpGet("{employeeId:guid}")]
    public async Task<IActionResult> GetAll(Guid employeeId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _notificationService.GetAllNotificationsAsync(employeeId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Mark a single notification as read.
    /// </summary>
    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken ct = default)
    {
        await _notificationService.MarkAsReadAsync(notificationId, ct);
        return NoContent();
    }

    /// <summary>
    /// Mark all notifications for an employee as read.
    /// </summary>
    [HttpPost("mark-all-read/{employeeId:guid}")]
    public async Task<IActionResult> MarkAllAsRead(Guid employeeId, CancellationToken ct = default)
    {
        await _notificationService.MarkAllAsReadAsync(employeeId, ct);
        return NoContent();
    }

    /// <summary>
    /// Get unread notification count for an employee.
    /// </summary>
    [HttpGet("unread-count/{employeeId:guid}")]
    public async Task<IActionResult> GetUnreadCount(Guid employeeId, CancellationToken ct = default)
    {
        var count = await _notificationService.GetUnreadCountAsync(employeeId, ct);
        return Ok(count);
    }
}
