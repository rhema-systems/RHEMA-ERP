using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AppraisalNotificationsController : ControllerBase
{
    private readonly IAppraisalNotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AppraisalNotificationsController> _logger;

    public AppraisalNotificationsController(
        IAppraisalNotificationService notificationService,
        ICurrentUserService currentUser,
        ILogger<AppraisalNotificationsController> logger)
    {
        _notificationService = notificationService;
        _currentUser         = currentUser;
        _logger              = logger;
    }

    // ── Signed-in employee ────────────────────────────────────────────────────
    // The routes below take an explicit employeeId, which a notification bell has no way to
    // know. These four resolve it from the token instead, and are what the UI calls.

    /// <summary>Notification bell summary for the signed-in employee.</summary>
    [HttpGet("me/summary")]
    public async Task<IActionResult> GetMySummary([FromQuery] int recentCount = 20, CancellationToken ct = default)
    {
        if (!TryGetEmployee(out var employeeId, out var problem)) return problem!;
        return Ok(await _notificationService.GetNotificationSummaryAsync(employeeId, recentCount, ct));
    }

    /// <summary>Paginated notifications for the signed-in employee.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetEmployee(out var employeeId, out var problem)) return problem!;
        return Ok(await _notificationService.GetAllNotificationsAsync(employeeId, page, pageSize, ct));
    }

    /// <summary>Unread count for the signed-in employee.</summary>
    [HttpGet("me/unread-count")]
    public async Task<IActionResult> GetMyUnreadCount(CancellationToken ct = default)
    {
        if (!TryGetEmployee(out var employeeId, out var problem)) return problem!;
        return Ok(await _notificationService.GetUnreadCountAsync(employeeId, ct));
    }

    /// <summary>Mark every notification for the signed-in employee as read.</summary>
    [HttpPost("me/mark-all-read")]
    public async Task<IActionResult> MarkAllMineAsRead(CancellationToken ct = default)
    {
        if (!TryGetEmployee(out var employeeId, out var problem)) return problem!;
        await _notificationService.MarkAllAsReadAsync(employeeId, ct);
        return NoContent();
    }

    /// <summary>
    /// Notifications are addressed to an employee record, so an account with no employee
    /// link has none — that is a 400 with an explanation, not an empty list that looks
    /// like "you are all caught up".
    /// </summary>
    private bool TryGetEmployee(out Guid employeeId, out IActionResult? problem)
    {
        var id = _currentUser.EmployeeId;
        if (id == null || id == Guid.Empty)
        {
            employeeId = Guid.Empty;
            problem = BadRequest(new { message = "Your user account is not linked to an employee record." });
            return false;
        }

        employeeId = id.Value;
        problem = null;
        return true;
    }

    // ── By employee id ────────────────────────────────────────────────────────

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
