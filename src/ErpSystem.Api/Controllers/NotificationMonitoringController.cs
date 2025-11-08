using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services.Maintenance;

namespace ErpSystem.Api.Controllers;

/// <summary>
/// Admin controller for monitoring notification queues, dead-letter messages, and background tasks.
/// Provides metrics, health checks, and management operations for the notification system.
/// </summary>
[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "SuperAdmin,TenantAdmin")]
public class NotificationMonitoringController : ControllerBase
{
    private readonly IDeadLetterNotificationService _deadLetterService;
    private readonly IMaintenanceEscalationService _escalationService;
    private readonly IMaintenanceNotificationTemplateService _templateService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<NotificationMonitoringController> _logger;

    public NotificationMonitoringController(
        IDeadLetterNotificationService deadLetterService,
        IMaintenanceEscalationService escalationService,
        IMaintenanceNotificationTemplateService templateService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<NotificationMonitoringController> logger)
    {
        _deadLetterService = deadLetterService;
        _escalationService = escalationService;
        _templateService = templateService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get overall notification system health metrics
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<object>> GetHealthMetrics()
    {
        try
        {
            var deadLetterStats = await _deadLetterService.GetDeadLetterStatisticsAsync();
            var pendingCount = await GetPendingNotificationCountAsync();
            var sentCount = await GetSentNotificationCountAsync();

            var health = new
            {
                Status = "Operational",
                Timestamp = DateTime.UtcNow,
                Metrics = new
                {
                    PendingNotifications = pendingCount,
                    SentNotifications = sentCount,
                    DeadLetters = deadLetterStats["TotalDeadLetters"],
                    CriticalIssues = deadLetterStats["Critical"],
                    HighPriority = deadLetterStats["High"]
                },
                Details = deadLetterStats
            };

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification health metrics");
            return StatusCode(500, new { error = "Failed to retrieve metrics" });
        }
    }

    /// <summary>
    /// Get pending notifications queue (paginated)
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<PagedResult<MaintenanceNotificationDto>>> GetPendingNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var repo = _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.MaintenanceNotification>();
            var pending = await repo.FindAsync(n => n.Status == "Pending");

            var pagedList = pending
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList();

            return Ok(new PagedResult<MaintenanceNotificationDto>
            {
                Items = pagedList,
                TotalCount = pending.Count(),
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending notifications");
            return StatusCode(500, new { error = "Failed to retrieve pending notifications" });
        }
    }

    /// <summary>
    /// Get dead-letter queue (notifications that failed after max retries)
    /// </summary>
    [HttpGet("dead-letters")]
    public async Task<ActionResult<PagedResult<MaintenanceNotificationDto>>> GetDeadLetterQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            return Ok(await _deadLetterService.GetDeadLetterNotificationsAsync(page, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dead-letter notifications");
            return StatusCode(500, new { error = "Failed to retrieve dead-letter queue" });
        }
    }

    /// <summary>
    /// Get dead-letter statistics by category
    /// </summary>
    [HttpGet("dead-letters/statistics")]
    public async Task<ActionResult<Dictionary<string, int>>> GetDeadLetterStatistics()
    {
        try
        {
            var stats = await _deadLetterService.GetDeadLetterStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dead-letter statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    /// <summary>
    /// Get dead-letters filtered by entity type
    /// </summary>
    [HttpGet("dead-letters/by-entity/{entityType}")]
    public async Task<ActionResult<List<MaintenanceNotificationDto>>> GetDeadLettersByEntityType(string entityType)
    {
        try
        {
            var deadLetters = await _deadLetterService.GetDeadLettersByEntityTypeAsync(entityType);
            return Ok(deadLetters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dead-letters for entity type {EntityType}", entityType);
            return StatusCode(500, new { error = "Failed to retrieve dead-letters" });
        }
    }

    /// <summary>
    /// Get dead-letters filtered by error type
    /// </summary>
    [HttpGet("dead-letters/by-error/{errorType}")]
    public async Task<ActionResult<List<MaintenanceNotificationDto>>> GetDeadLettersByErrorType(string errorType)
    {
        try
        {
            var deadLetters = await _deadLetterService.GetDeadLettersByErrorTypeAsync(errorType);
            return Ok(deadLetters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dead-letters for error type {ErrorType}", errorType);
            return StatusCode(500, new { error = "Failed to retrieve dead-letters" });
        }
    }

    /// <summary>
    /// Retry a specific dead-letter notification
    /// </summary>
    [HttpPost("dead-letters/{notificationId}/retry")]
    public async Task<ActionResult> RetryDeadLetter(Guid notificationId)
    {
        try
        {
            var count = await _deadLetterService.RetryDeadLetterNotificationAsync(notificationId);
            _logger.LogInformation("Retried dead-letter notification {NotificationId}", notificationId);
            return Ok(new { message = "Notification moved to pending queue for retry", retryCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying dead-letter {NotificationId}", notificationId);
            return StatusCode(500, new { error = "Failed to retry notification" });
        }
    }

    /// <summary>
    /// Retry all dead-letter notifications
    /// </summary>
    [HttpPost("dead-letters/retry-all")]
    public async Task<ActionResult> RetryAllDeadLetters()
    {
        try
        {
            var count = await _deadLetterService.RetryAllDeadLettersAsync();
            _logger.LogInformation("Retried all dead-letter notifications: {Count}", count);
            return Ok(new { message = $"Moved {count} notifications to pending queue", retryCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying all dead-letters");
            return StatusCode(500, new { error = "Failed to retry notifications" });
        }
    }

    /// <summary>
    /// Delete a dead-letter notification permanently
    /// </summary>
    [HttpDelete("dead-letters/{notificationId}")]
    public async Task<ActionResult> DeleteDeadLetter(Guid notificationId)
    {
        try
        {
            await _deadLetterService.DeleteDeadLetterPermanentlyAsync(notificationId);
            _logger.LogInformation("Deleted dead-letter notification {NotificationId}", notificationId);
            return Ok(new { message = "Notification deleted permanently" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting dead-letter {NotificationId}", notificationId);
            return StatusCode(500, new { error = "Failed to delete notification" });
        }
    }

    /// <summary>
    /// Get all escalation rules
    /// </summary>
    [HttpGet("escalation-rules")]
    public async Task<ActionResult<List<MaintenanceEscalationRuleDto>>> GetEscalationRules()
    {
        try
        {
            var rules = await _escalationService.GetAllRulesAsync();
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving escalation rules");
            return StatusCode(500, new { error = "Failed to retrieve escalation rules" });
        }
    }

    /// <summary>
    /// Manually trigger escalation evaluation
    /// </summary>
    [HttpPost("escalation-rules/evaluate")]
    public async Task<ActionResult> EvaluateEscalations()
    {
        try
        {
            await _escalationService.EvaluateAndExecuteEscalationsAsync();
            _logger.LogInformation("Manual escalation evaluation triggered");
            return Ok(new { message = "Escalation evaluation completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating escalations");
            return StatusCode(500, new { error = "Failed to evaluate escalations" });
        }
    }

    /// <summary>
    /// Get all notification templates
    /// </summary>
    [HttpGet("templates")]
    public async Task<ActionResult<List<MaintenanceNotificationTemplateDto>>> GetTemplates()
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates");
            return StatusCode(500, new { error = "Failed to retrieve templates" });
        }
    }

    /// <summary>
    /// Get notification system configuration
    /// </summary>
    [HttpGet("configuration")]
    public ActionResult<object> GetConfiguration()
    {
        try
        {
            var config = new
            {
                MaxRetryAttempts = 5,
                DispatchIntervalMinutes = 5,
                InitialBackoffSeconds = 30,
                BackoffMultiplier = 1.5,
                DeadLetterThreshold = 5,
                Status = "Active"
            };

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration");
            return StatusCode(500, new { error = "Failed to retrieve configuration" });
        }
    }

    private MaintenanceNotificationDto MapToDto(ErpSystem.Core.Entities.Maintenance.MaintenanceNotification entity)
    {
        return new MaintenanceNotificationDto
        {
            Id = entity.Id,
            NotificationType = entity.NotificationType,
            EntityType = entity.EntityType,
            EntityId = entity.EntityId,
            RecipientId = entity.RecipientId,
            RecipientRole = entity.RecipientRole,
            Title = entity.Title,
            Message = entity.Message,
            Priority = entity.Priority,
            Status = entity.Status,
            ScheduledFor = entity.ScheduledFor,
            SentAt = entity.SentAt,
            AdditionalData = entity.AdditionalData,
            ActionUrl = entity.ActionUrl,
            CreatedDate = entity.CreatedAt,
            AttemptCount = entity.AttemptCount,
            LastError = entity.LastError
        };
    }

    private async Task<int> GetPendingNotificationCountAsync()
    {
        try
        {
            var repo = _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.MaintenanceNotification>();
            var pending = await repo.FindAsync(n => n.Status == "Pending");
            return pending.Count();
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> GetSentNotificationCountAsync()
    {
        try
        {
            var repo = _unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.MaintenanceNotification>();
            var sent = await repo.FindAsync(n => n.Status == "Sent");
            return sent.Count();
        }
        catch
        {
            return 0;
        }
    }
}
