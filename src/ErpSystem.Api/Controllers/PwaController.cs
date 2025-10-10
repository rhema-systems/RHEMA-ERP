using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Core.DTOs.Notifications;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PwaController : ControllerBase
    {
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<PwaController> _logger;

        public PwaController(
            IPushNotificationService pushNotificationService,
            ICurrentUserService currentUserService,
            ILogger<PwaController> logger)
        {
            _pushNotificationService = pushNotificationService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get VAPID public key for PWA push notifications
        /// </summary>
        [HttpGet("vapid-public-key")]
        public async Task<ActionResult<string>> GetVapidPublicKey()
        {
            try
            {
                var publicKey = await _pushNotificationService.GetPublicVapidKeyAsync();
                return Ok(new { publicKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving VAPID public key");
                return StatusCode(500, "An error occurred while retrieving VAPID public key");
            }
        }

        /// <summary>
        /// Subscribe to push notifications
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<ActionResult> Subscribe([FromBody] PushSubscriptionDto subscription)
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }
                
                var success = await _pushNotificationService.SaveSubscriptionAsync(userId.Value, subscription);
                
                if (success)
                {
                    return Ok(new { message = "Successfully subscribed to push notifications" });
                }
                
                return BadRequest("Failed to subscribe to push notifications");
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
        public async Task<ActionResult> Unsubscribe([FromBody] PushSubscriptionDto subscription)
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }
                
                var success = await _pushNotificationService.RemoveSubscriptionAsync(userId.Value, subscription.Endpoint);
                
                if (success)
                {
                    return Ok(new { message = "Successfully unsubscribed from push notifications" });
                }
                
                return BadRequest("Failed to unsubscribe from push notifications");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from push notifications");
                return StatusCode(500, "An error occurred while unsubscribing from push notifications");
            }
        }

        /// <summary>
        /// Test push notification
        /// </summary>
        [HttpPost("test-notification")]
        public async Task<ActionResult> TestNotification([FromBody] TestNotificationDto testDto)
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }
                
                var success = await _pushNotificationService.TestPushNotificationAsync(userId.Value, testDto.Message);
                
                if (success)
                {
                    return Ok(new { message = "Test notification sent successfully" });
                }
                
                return BadRequest("Failed to send test notification");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, "An error occurred while sending test notification");
            }
        }

        /// <summary>
        /// Get push notification health status
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<PushNotificationHealthDto>> GetHealth()
        {
            try
            {
                var health = await _pushNotificationService.GetHealthStatusAsync();
                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving push notification health");
                return StatusCode(500, "An error occurred while retrieving health status");
            }
        }

        /// <summary>
        /// Get user's push subscriptions
        /// </summary>
        [HttpGet("subscriptions")]
        public async Task<ActionResult<List<PushSubscriptionDto>>> GetSubscriptions()
        {
            try
            {
                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }
                
                var subscriptions = await _pushNotificationService.GetUserSubscriptionsAsync(userId.Value);
                return Ok(subscriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user subscriptions");
                return StatusCode(500, "An error occurred while retrieving subscriptions");
            }
        }

        /// <summary>
        /// Validate push subscription
        /// </summary>
        [HttpPost("validate-subscription")]
        public async Task<ActionResult<bool>> ValidateSubscription([FromBody] PushSubscriptionDto subscription)
        {
            try
            {
                var isValid = await _pushNotificationService.ValidateSubscriptionAsync(subscription);
                return Ok(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating subscription");
                return StatusCode(500, "An error occurred while validating subscription");
            }
        }

        /// <summary>
        /// Get delivery statistics (Admin only)
        /// </summary>
        [HttpGet("delivery-stats")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<NotificationDeliveryStatsDto>> GetDeliveryStats(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var stats = await _pushNotificationService.GetDeliveryStatsAsync(tenantId.Value, from, to);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving delivery statistics");
                return StatusCode(500, "An error occurred while retrieving delivery statistics");
            }
        }

        /// <summary>
        /// Get failed notifications (Admin only)
        /// </summary>
        [HttpGet("failed-notifications")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult<List<FailedPushNotificationDto>>> GetFailedNotifications(
            [FromQuery] int maxResults = 100)
        {
            try
            {
                var failed = await _pushNotificationService.GetFailedNotificationsAsync(maxResults);
                return Ok(failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving failed notifications");
                return StatusCode(500, "An error occurred while retrieving failed notifications");
            }
        }
    }

    public class TestNotificationDto
    {
        public string Message { get; set; } = "Test notification from ERP System";
    }
}
