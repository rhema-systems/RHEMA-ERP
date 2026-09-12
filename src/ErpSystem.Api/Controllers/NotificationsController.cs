using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get notifications for current user
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<NotificationDto>>> GetNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? unreadOnly = null,
            [FromQuery] string? type = null,
            [FromQuery] string? severity = null)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var notifications = await _notificationService.GetNotificationsAsync(
                    userId.Value, tenantId.Value, page, pageSize, unreadOnly, type, severity);

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications");
                return StatusCode(500, "An error occurred while retrieving notifications");
            }
        }

        /// <summary>
        /// Get unread notifications
        /// </summary>
        [HttpGet("unread")]
        public async Task<ActionResult<List<NotificationDto>>> GetUnreadNotifications(
            [FromQuery] int limit = 10)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var notifications = await _notificationService.GetNotificationsAsync(
                    userId.Value, tenantId.Value, 1, limit, unreadOnly: true);

                return Ok(notifications.Items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread notifications");
                return StatusCode(500, "An error occurred while retrieving unread notifications");
            }
        }

        /// <summary>
        /// Get unread notification count
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var count = await _notificationService.GetUnreadCountAsync(userId.Value, tenantId.Value);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread count");
                return StatusCode(500, "An error occurred while retrieving unread count");
            }
        }

        /// <summary>
        /// Mark notification as read
        /// </summary>
        [HttpPost("{notificationId:guid}/mark-read")]
        public async Task<ActionResult> MarkAsRead(Guid notificationId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var success = await _notificationService.MarkAsReadAsync(notificationId, userId.Value, tenantId.Value);
                if (!success)
                {
                    return NotFound();
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read: {NotificationId}", notificationId);
                return StatusCode(500, "An error occurred while marking notification as read");
            }
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        [HttpPost("mark-all-read")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var count = await _notificationService.MarkAllAsReadAsync(userId.Value, tenantId.Value);
                return Ok(new { markedCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return StatusCode(500, "An error occurred while marking notifications as read");
            }
        }

        /// <summary>
        /// Delete a notification
        /// </summary>
        [HttpDelete("{notificationId:guid}")]
        public async Task<ActionResult> DeleteNotification(Guid notificationId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var success = await _notificationService.DeleteNotificationAsync(notificationId, userId.Value, tenantId.Value);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification: {NotificationId}", notificationId);
                return StatusCode(500, "An error occurred while deleting notification");
            }
        }

        /// <summary>
        /// Create a notification
        /// </summary>
        /// <remarks>
        /// Admin-gated (HR area-25 slice 11). This action takes an arbitrary RecipientId,
        /// title, message and actionUrl — under the controller's plain [Authorize] ANY user
        /// could plant an official-looking notification (an "Approval Required" with a chosen
        /// link, say) in ANY other user's feed, measured live. Backend modules create
        /// notifications through INotificationService directly, and no frontend flow calls
        /// this route (the only client wrapper, notificationService.sendNotification, has no
        /// callers) — so the gate matches send-push's and breaks nothing.
        /// </remarks>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<NotificationDto>> CreateNotification(CreateNotificationDto createNotificationDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var notification = await _notificationService.CreateNotificationAsync(
                    createNotificationDto, userId.Value, tenantId.Value);

                return CreatedAtAction(nameof(GetNotification), new { notificationId = notification.Id }, notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification");
                return StatusCode(500, "An error occurred while creating notification");
            }
        }

        /// <summary>
        /// Get a specific notification
        /// </summary>
        [HttpGet("{notificationId:guid}")]
        public async Task<ActionResult<NotificationDto>> GetNotification(Guid notificationId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var notification = await _notificationService.GetNotificationAsync(notificationId, userId.Value, tenantId.Value);
                if (notification == null)
                {
                    return NotFound();
                }

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification: {NotificationId}", notificationId);
                return StatusCode(500, "An error occurred while retrieving notification");
            }
        }

        /// <summary>
        /// Send push notification (Admin only)
        /// </summary>
        [HttpPost("send-push")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult> SendPushNotification(SendPushNotificationDto pushNotificationDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                await _notificationService.SendPushNotificationAsync(pushNotificationDto, userId.Value, tenantId.Value);
                return Ok(new { message = "Push notification sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification");
                return StatusCode(500, "An error occurred while sending push notification");
            }
        }

        /// <summary>
        /// Get notification preferences
        /// </summary>
        [HttpGet("preferences")]
        public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences()
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var preferences = await _notificationService.GetPreferencesAsync(userId.Value);
                return Ok(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification preferences");
                return StatusCode(500, "An error occurred while retrieving notification preferences");
            }
        }

        /// <summary>
        /// Update notification preferences
        /// </summary>
        [HttpPut("preferences")]
        public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferences(UpdateNotificationPreferencesDto preferencesDto)
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var preferences = await _notificationService.UpdatePreferencesAsync(userId.Value, preferencesDto);
                return Ok(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification preferences");
                return StatusCode(500, "An error occurred while updating notification preferences");
            }
        }

        /// <summary>
        /// Subscribe to push notifications
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<ActionResult> Subscribe(PushSubscriptionDto subscriptionDto)
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                await _notificationService.SubscribeToPushNotificationsAsync(userId.Value, subscriptionDto);
                return Ok(new { message = "Successfully subscribed to push notifications" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to push notifications");
                return StatusCode(500, "An error occurred while subscribing to push notifications");
            }
        }

        /// <summary>
        /// Unsubscribe from push notifications
        /// </summary>
        [HttpPost("unsubscribe")]
        public async Task<ActionResult> Unsubscribe(PushSubscriptionDto subscriptionDto)
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                await _notificationService.UnsubscribeFromPushNotificationsAsync(userId.Value, subscriptionDto);
                return Ok(new { message = "Successfully unsubscribed from push notifications" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from push notifications");
                return StatusCode(500, "An error occurred while unsubscribing from push notifications");
            }
        }

        /// <summary>
        /// Get notification statistics (Admin only)
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<NotificationStatisticsDto>> GetStatistics(
            [FromQuery] string period = "last-30-days")
        {
            try
            {
                var tenantId = _currentUserService.TenantId;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var statistics = await _notificationService.GetStatisticsAsync(tenantId.Value, period, isSuperAdmin);

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification statistics");
                return StatusCode(500, "An error occurred while retrieving notification statistics");
            }
        }

        /// <summary>
        /// Get notification templates (Admin only)
        /// </summary>
        [HttpGet("templates")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<List<NotificationTemplateDto>>> GetTemplates([FromQuery] string? type = null)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var templates = await _notificationService.GetNotificationTemplatesAsync(tenantId.Value, type);
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notification templates");
                return StatusCode(500, "An error occurred while retrieving templates");
            }
        }

        /// <summary>
        /// Create a notification template (Admin only)
        /// </summary>
        [HttpPost("templates")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<NotificationTemplateDto>> CreateTemplate(CreateNotificationTemplateDto templateDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var template = await _notificationService.CreateNotificationTemplateAsync(templateDto, userId.Value, tenantId.Value);
                return CreatedAtAction(nameof(GetTemplates), new { id = template.Id }, template);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification template");
                return StatusCode(500, "An error occurred while creating template");
            }
        }

        /// <summary>
        /// Update a notification template (Admin only)
        /// </summary>
        [HttpPut("templates/{templateId:guid}")]
        [HttpPost("templates/{templateId:guid}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<NotificationTemplateDto>> UpdateTemplate(Guid templateId, UpdateNotificationTemplateDto templateDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var template = await _notificationService.UpdateNotificationTemplateAsync(templateId, templateDto, userId.Value, tenantId.Value);
                if (template == null) return NotFound();
                return Ok(template);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification template {TemplateId}", templateId);
                return StatusCode(500, "An error occurred while updating template");
            }
        }

        /// <summary>
        /// Delete a notification template (Admin only)
        /// </summary>
        [HttpDelete("templates/{templateId:guid}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult> DeleteTemplate(Guid templateId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var ok = await _notificationService.DeleteNotificationTemplateAsync(templateId, tenantId.Value);
                if (!ok) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification template {TemplateId}", templateId);
                return StatusCode(500, "An error occurred while deleting template");
            }
        }

        /// <summary>
        /// Create email campaign (Admin only)
        /// </summary>
        [HttpPost("campaigns")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<EmailCampaignDto>> CreateEmailCampaign(CreateEmailCampaignDto campaignDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var campaign = await _notificationService.CreateEmailCampaignAsync(campaignDto, userId.Value, tenantId.Value);
                return CreatedAtAction(nameof(GetEmailCampaign), new { campaignId = campaign.Id }, campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating email campaign");
                return StatusCode(500, "An error occurred while creating email campaign");
            }
        }

        /// <summary>
        /// Get email campaign
        /// </summary>
        [HttpGet("campaigns/{campaignId:guid}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<EmailCampaignDto>> GetEmailCampaign(Guid campaignId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var campaign = await _notificationService.GetEmailCampaignAsync(campaignId, tenantId.Value);
                if (campaign == null)
                {
                    return NotFound();
                }

                return Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving email campaign: {CampaignId}", campaignId);
                return StatusCode(500, "An error occurred while retrieving email campaign");
            }
        }

        /// <summary>
        /// Get email campaigns
        /// </summary>
        [HttpGet("campaigns")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<List<EmailCampaignDto>>> GetEmailCampaigns(
            [FromQuery] string? status = null)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var campaigns = await _notificationService.GetEmailCampaignsAsync(tenantId.Value, status);
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving email campaigns");
                return StatusCode(500, "An error occurred while retrieving email campaigns");
            }
        }

        /// <summary>
        /// Send email campaign (Admin only)
        /// </summary>
        [HttpPost("campaigns/{campaignId:guid}/send")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<EmailCampaignDto>> SendEmailCampaign(Guid campaignId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var campaign = await _notificationService.SendEmailCampaignAsync(campaignId, tenantId.Value);
                if (campaign == null) return NotFound();

                return Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email campaign {CampaignId}", campaignId);
                return StatusCode(500, "An error occurred while sending campaign");
            }
        }

        /// <summary>
        /// Delete email campaign (Admin only)
        /// </summary>
        [HttpDelete("campaigns/{campaignId:guid}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult> DeleteEmailCampaign(Guid campaignId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var ok = await _notificationService.DeleteEmailCampaignAsync(campaignId, tenantId.Value);
                if (!ok) return NotFound();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting email campaign {CampaignId}", campaignId);
                return StatusCode(500, "An error occurred while deleting email campaign");
            }
        }

        /// <summary>
        /// Create test notifications (Development only)
        /// </summary>
        [HttpPost("dev/create-test-notifications")]
        public async Task<ActionResult> CreateTestNotifications()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                // Create sample notifications (ActionUrl is optional - will be auto-derived on frontend from ENTITY_CONFIG)
                var testNotifications = new List<CreateNotificationDto>
                {
                    new CreateNotificationDto
                    {
                        RecipientId = userId.Value,
                        Type = "InApp",
                        Title = "Test Notification - Critical",
                        Message = "This is a test critical priority notification to verify the notification system is working.",
                        Priority = "Critical",
                        EntityType = "JobCard",
                        EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111")
                        // ActionUrl is optional - frontend will auto-derive: /maintenance/job-cards/{id}
                    },
                    new CreateNotificationDto
                    {
                        RecipientId = userId.Value,
                        Type = "InApp",
                        Title = "Test Notification - High",
                        Message = "This is a test high priority notification from a WorkOrder.",
                        Priority = "High",
                        EntityType = "WorkOrder",
                        EntityId = Guid.Parse("22222222-2222-2222-2222-222222222222")
                        // ActionUrl is optional - frontend will auto-derive: /maintenance/work-orders/{id}
                    },
                    new CreateNotificationDto
                    {
                        RecipientId = userId.Value,
                        Type = "InApp",
                        Title = "Test Notification - Normal",
                        Message = "This is a test normal priority notification.",
                        Priority = "Normal",
                        EntityType = "Quality",
                        EntityId = Guid.Parse("33333333-3333-3333-3333-333333333333")
                        // ActionUrl is optional - frontend will auto-derive: /maintenance/quality-control/{id}
                    },
                    new CreateNotificationDto
                    {
                        RecipientId = userId.Value,
                        Type = "InApp",
                        Title = "Test Notification - Low",
                        Message = "This is a test low priority notification.",
                        Priority = "Low",
                        EntityType = "Asset",
                        EntityId = Guid.Parse("44444444-4444-4444-4444-444444444444")
                        // ActionUrl is optional - frontend will auto-derive: /maintenance/assets/{id}
                    }
                };

                var created = new List<NotificationDto>();
                foreach (var notificationDto in testNotifications)
                {
                    var result = await _notificationService.CreateNotificationAsync(notificationDto, userId.Value, tenantId.Value);
                    created.Add(result);
                }

                _logger.LogInformation("Created {Count} test notifications for user {UserId}", created.Count, userId.Value);
                return Ok(new { message = $"Created {created.Count} test notifications", notifications = created });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test notifications");
                return StatusCode(500, "An error occurred while creating test notifications");
            }
        }
    }
}
