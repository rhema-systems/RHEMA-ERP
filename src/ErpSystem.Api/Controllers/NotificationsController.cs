using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;

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
        /// Create a notification (Admin only)
        /// </summary>
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
    }
}
