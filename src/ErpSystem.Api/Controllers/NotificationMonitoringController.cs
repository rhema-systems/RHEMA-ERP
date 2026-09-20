using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Services.Maintenance;
using ErpSystem.Data;
using ErpSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

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
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IHubNotificationService _hubNotificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationMonitoringController> _logger;

    public NotificationMonitoringController(
        IDeadLetterNotificationService deadLetterService,
        IMaintenanceEscalationService escalationService,
        IMaintenanceNotificationTemplateService templateService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ApplicationDbContext dbContext,
        IEmailService emailService,
        IHubNotificationService hubNotificationService,
        IConfiguration configuration,
        ILogger<NotificationMonitoringController> logger)
    {
        _deadLetterService = deadLetterService;
        _escalationService = escalationService;
        _templateService = templateService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
        _emailService = emailService;
        _hubNotificationService = hubNotificationService;
        _configuration = configuration;
        _logger = logger;
    }

    // -----------------------------------------------------------------------------
    // Message Queue (Unified Notification table: email + in-app + other channels)
    // -----------------------------------------------------------------------------

    [HttpGet("message-queue")]
    public async Task<ActionResult<ErpSystem.Core.DTOs.Common.PagedResult<AdminMessageQueueItemDto>>> GetMessageQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? search = null)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var query = _unitOfWork.Repository<Notification>()
                .GetQueryable()
                .AsNoTracking()
                .Where(n => n.TenantId == tenantId && !n.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(n => n.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(channel))
            {
                query = query.Where(n => n.DeliveryMethods != null && n.DeliveryMethods.Contains(channel));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(n =>
                    (n.Title != null && n.Title.Contains(search)) ||
                    (n.Message != null && n.Message.Contains(search)) ||
                    (n.EmailAddress != null && n.EmailAddress.Contains(search)) ||
                    (n.NotificationType != null && n.NotificationType.Contains(search)) ||
                    (n.EntityType != null && n.EntityType.Contains(search)));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var recipientIds = items
                .Select(n => n.RecipientId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var users = recipientIds.Count == 0
                ? new Dictionary<Guid, (string? Name, string? Email)>()
                : await _dbContext.Users
                    .AsNoTracking()
                    .Where(u => recipientIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName, u.Email })
                    .ToDictionaryAsync(x => x.Id, x => (Name: (string?)x.UserName, Email: (string?)x.Email));

            var dtoItems = items.Select(n =>
            {
                users.TryGetValue(n.RecipientId, out var user);
                return new AdminMessageQueueItemDto
                {
                    Id = n.Id,
                    NotificationType = n.NotificationType,
                    Title = n.Title,
                    MessagePreview = Truncate(n.Message, 200),
                    Status = n.Status,
                    Priority = n.Priority,
                    DeliveryMethods = n.DeliveryMethods,
                    RecipientId = n.RecipientId == Guid.Empty ? null : n.RecipientId,
                    RecipientName = n.RecipientId == Guid.Empty ? null : user.Name,
                    RecipientEmail = n.RecipientId == Guid.Empty ? null : user.Email,
                    EmailAddress = n.EmailAddress,
                    PhoneNumber = n.PhoneNumber,
                    AttemptCount = n.AttemptCount,
                    LastError = n.LastError,
                    EntityType = n.EntityType,
                    EntityId = n.EntityId,
                    ActionUrl = n.ActionUrl,
                    CreatedAt = n.CreatedAt,
                    SentAt = n.SentAt
                };
            }).ToList();

            return Ok(new ErpSystem.Core.DTOs.Common.PagedResult<AdminMessageQueueItemDto>
            {
                Items = dtoItems,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving message queue items");
            return StatusCode(500, new { error = "Failed to retrieve message queue items" });
        }
    }

    [HttpGet("message-queue/{notificationId:guid}")]
    public async Task<ActionResult<AdminMessageQueueItemDetailDto>> GetMessageQueueItem(Guid notificationId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var repo = _unitOfWork.Repository<Notification>();
            var n = await repo.GetByIdAsync(notificationId);

            if (n == null || n.TenantId != tenantId || n.IsDeleted) return NotFound();

            var user = n.RecipientId == Guid.Empty
                ? null
                : await _dbContext.Users.AsNoTracking()
                    .Where(u => u.Id == n.RecipientId)
                    .Select(u => new { u.UserName, u.Email })
                    .FirstOrDefaultAsync();

            return Ok(new AdminMessageQueueItemDetailDto
            {
                Id = n.Id,
                NotificationType = n.NotificationType,
                Title = n.Title,
                Message = n.Message,
                Status = n.Status,
                Priority = n.Priority,
                DeliveryMethods = n.DeliveryMethods,
                RecipientId = n.RecipientId == Guid.Empty ? null : n.RecipientId,
                RecipientName = user?.UserName,
                RecipientEmail = user?.Email,
                EmailAddress = n.EmailAddress,
                PhoneNumber = n.PhoneNumber,
                AttemptCount = n.AttemptCount,
                LastError = n.LastError,
                EntityType = n.EntityType,
                EntityId = n.EntityId,
                ActionUrl = n.ActionUrl,
                AdditionalData = n.AdditionalData,
                ScheduledFor = n.ScheduledFor,
                CreatedAt = n.CreatedAt,
                SentAt = n.SentAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving message queue item {NotificationId}", notificationId);
            return StatusCode(500, new { error = "Failed to retrieve message details" });
        }
    }

    [HttpPost("message-queue/{notificationId:guid}/retry")]
    public async Task<ActionResult> RetryMessageQueueItem(Guid notificationId)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var repo = _unitOfWork.Repository<Notification>();
        var n = await repo.GetByIdAsync(notificationId);

        if (n == null || n.TenantId != tenantId || n.IsDeleted) return NotFound();

        if (n.Message == UnifiedNotificationService.RedactedEmailAuditBody)
        {
            return BadRequest(new
            {
                error = "Sensitive email content was not retained. Retry delivery from the originating workflow to generate a fresh secret; the audit placeholder cannot be emailed."
            });
        }

        // Avoid accidental duplicate sends.
        if (string.Equals(n.Status, "Sent", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Notification has already been sent." });
        }

        if (!string.Equals(n.Status, "Pending", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(n.Status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Cannot retry notification with status '{n.Status}'." });
        }

        // Mark as processing to reduce the chance the background dispatcher picks it up simultaneously.
        n.Status = "Processing";
        n.AttemptCount += 1;
        n.LastError = null;
        n.ScheduledFor = DateTime.UtcNow;

        await repo.UpdateAsync(n);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            var sentAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(n.DeliveryMethods) &&
                n.DeliveryMethods.Contains("Email", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(n.EmailAddress))
                {
                    n.Status = "Failed";
                    n.LastError = "Cannot retry email: missing recipient email address";
                    await repo.UpdateAsync(n);
                    await _unitOfWork.SaveChangesAsync();
                    return BadRequest(new { error = n.LastError });
                }

                var emailPayload = TryParseEmailPayload(n.AdditionalData);

                var emailDto = new ErpSystem.Core.Interfaces.Common.EmailDto
                {
                    To = n.EmailAddress,
                    Subject = emailPayload?.Subject ?? n.Title ?? string.Empty,
                    Body = emailPayload?.BodyHtml ?? n.Message ?? string.Empty,
                    IsHtml = emailPayload?.IsHtml ?? true,
                    Attachments = emailPayload?.Attachments?.Select(a => new ErpSystem.Core.Interfaces.Common.EmailAttachmentDto
                    {
                        FileName = a.FileName ?? string.Empty,
                        ContentType = a.ContentType ?? string.Empty,
                        Content = string.IsNullOrWhiteSpace(a.ContentBase64) ? Array.Empty<byte>() : Convert.FromBase64String(a.ContentBase64)
                    }).Where(a => a.Content.Length > 0).ToList() ?? new List<ErpSystem.Core.Interfaces.Common.EmailAttachmentDto>()
                };

                await _emailService.SendEmailAsync(emailDto);

                n.Status = "Sent";
                n.SentAt = sentAt;
            }
            else if (!string.IsNullOrWhiteSpace(n.DeliveryMethods) &&
                     n.DeliveryMethods.Contains("InApp", StringComparison.OrdinalIgnoreCase))
            {
                if (n.RecipientId == Guid.Empty)
                {
                    n.Status = "Failed";
                    n.LastError = "Cannot retry in-app notification: missing recipient user id";
                    await repo.UpdateAsync(n);
                    await _unitOfWork.SaveChangesAsync();
                    return BadRequest(new { error = n.LastError });
                }

                var dashboardNotification = new ErpSystem.Core.DTOs.Dashboard.NotificationDto
                {
                    Id = n.Id.ToString(),
                    Type = n.NotificationType,
                    Title = n.Title,
                    Message = n.Message,
                    Severity = n.Priority,
                    Timestamp = n.CreatedAt,
                    IsRead = n.IsRead,
                    ActionUrl = n.ActionUrl,
                    Metadata = n.AdditionalData != null
                        ? JsonSerializer.Deserialize<Dictionary<string, object>>(n.AdditionalData)
                        : null
                };

                await _hubNotificationService.BroadcastNotificationAsync(n.RecipientId.ToString(), dashboardNotification);

                n.Status = "Sent";
                n.SentAt = sentAt;
            }
            else
            {
                n.Status = "Failed";
                n.LastError = "Retry not supported for this notification channel";
                await repo.UpdateAsync(n);
                await _unitOfWork.SaveChangesAsync();
                return BadRequest(new { error = n.LastError });
            }

            await repo.UpdateAsync(n);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying message queue item {NotificationId}", notificationId);

            try
            {
                n.Status = "Failed";
                n.LastError = ex.Message;
                await repo.UpdateAsync(n);
                await _unitOfWork.SaveChangesAsync();
            }
            catch
            {
                // Best-effort: preserve original exception response.
            }

            return StatusCode(500, new { error = "Failed to retry notification" });
        }
    }

    [HttpDelete("message-queue/{notificationId:guid}")]
    public async Task<ActionResult> DeleteMessageQueueItem(Guid notificationId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var repo = _unitOfWork.Repository<Notification>();
            var n = await repo.GetByIdAsync(notificationId);

            if (n == null || n.TenantId != tenantId || n.IsDeleted) return NotFound();

            n.IsDeleted = true;
            n.DeletedAt = DateTime.UtcNow;

            await repo.UpdateAsync(n);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message queue item {NotificationId}", notificationId);
            return StatusCode(500, new { error = "Failed to delete notification" });
        }
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
            var maxRetryAttempts = int.TryParse(_configuration["Notifications:MaxRetryAttempts"], out var max) ? max : 5;
            var dispatchIntervalMinutes = int.TryParse(_configuration["Notifications:DispatchIntervalMinutes"], out var interval) ? interval : 5;
            var initialBackoffSeconds = int.TryParse(_configuration["Notifications:InitialBackoffSeconds"], out var backoff) ? backoff : 30;
            var backoffMultiplier = double.TryParse(_configuration["Notifications:BackoffMultiplier"], out var mult) ? mult : 1.5;

            var config = new
            {
                MaxRetryAttempts = maxRetryAttempts,
                DispatchIntervalMinutes = dispatchIntervalMinutes,
                InitialBackoffSeconds = initialBackoffSeconds,
                BackoffMultiplier = backoffMultiplier,
                DeadLetterThreshold = maxRetryAttempts,
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

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var unifiedPending = 0;
            if (tenantId != Guid.Empty)
            {
                unifiedPending = await _unitOfWork.Repository<Notification>()
                    .CountAsync(n => n.TenantId == tenantId && !n.IsDeleted && n.Status == "Pending");
            }

            return pending.Count() + unifiedPending;
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

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var unifiedSent = 0;
            if (tenantId != Guid.Empty)
            {
                unifiedSent = await _unitOfWork.Repository<Notification>()
                    .CountAsync(n => n.TenantId == tenantId && !n.IsDeleted && n.Status == "Sent");
            }

            return sent.Count() + unifiedSent;
        }
        catch
        {
            return 0;
        }
    }

    private static string Truncate(string? input, int maxLength)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (input.Length <= maxLength) return input;
        return input.Substring(0, maxLength) + "...";
    }

    private static EmailPayload? TryParseEmailPayload(string? additionalData)
    {
        if (string.IsNullOrWhiteSpace(additionalData)) return null;

        try
        {
            using var doc = JsonDocument.Parse(additionalData);

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("email", out var emailElem) &&
                emailElem.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<EmailPayload>(
                    emailElem.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

public class AdminMessageQueueItemDto
{
    public Guid Id { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string? DeliveryMethods { get; set; }
    public Guid? RecipientId { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class AdminMessageQueueItemDetailDto
{
    public Guid Id { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string? DeliveryMethods { get; set; }
    public Guid? RecipientId { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? ActionUrl { get; set; }
    public string? AdditionalData { get; set; }
    public DateTime ScheduledFor { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class EmailPayload
{
    public bool IsHtml { get; set; } = true;
    public string? Subject { get; set; }
    public string? BodyHtml { get; set; }
    public string? TextBody { get; set; }
    public List<EmailPayloadAttachment>? Attachments { get; set; }
}

public class EmailPayloadAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string? ContentBase64 { get; set; }
}
