using ErpSystem.Core.DTOs.Notifications;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Email attachment information for sending emails with attachments
/// </summary>
public class EmailAttachmentInfo
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}

/// <summary>
/// Unified notification service interface for the entire ERP system.
/// Single consolidated interface for all notification operations across all modules (maintenance, HR, procurement, etc.).
/// All modules use this interface instead of creating their own notification interfaces.
/// </summary>
public interface INotificationService
{
    #region Direct Notifications - Core Communication Channels

    /// <summary>
    /// Sends email notification to recipients
    /// </summary>
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);

    /// <summary>
    /// Sends email notification with attachments
    /// </summary>
    Task SendEmailWithAttachmentsAsync(string to, string subject, string body, List<EmailAttachmentInfo> attachments, bool isHtml = true);

    /// <summary>
    /// Sends SMS notification to phone number
    /// </summary>
    Task SendSmsAsync(string phoneNumber, string message);

    /// <summary>
    /// Sends push notification to user with optional data payload
    /// </summary>
    Task SendPushNotificationAsync(Guid userId, string title, string message, Dictionary<string, string>? data = null);

    /// <summary>
    /// Sends push notification using DTO with metadata
    /// </summary>
    Task SendPushNotificationAsync(SendPushNotificationDto pushNotificationDto, Guid sentBy, Guid tenantId);

    /// <summary>
    /// Creates in-app notification visible in the application UI
    /// </summary>
    Task CreateInAppNotificationAsync(Guid userId, string title, string message, string type, Dictionary<string, object>? data = null);

    /// <summary>
    /// Creates in-app notification with explicit tenant context (for background service)
    /// </summary>
    Task CreateInAppNotificationAsync(Guid userId, string title, string message, string type, Dictionary<string, object>? data, Guid tenantId);

    #endregion

    #region Notification CRUD Operations

    /// <summary>
    /// Retrieves paginated notifications for a user with optional filtering
    /// </summary>
    Task<ErpSystem.Core.DTOs.Notifications.PagedResult<NotificationDto>> GetNotificationsAsync(
        Guid userId, Guid tenantId, int page = 1, int pageSize = 20,
        bool? unreadOnly = null, string? type = null, string? severity = null);

    /// <summary>
    /// Retrieves a specific notification by ID
    /// </summary>
    Task<NotificationDto?> GetNotificationAsync(Guid notificationId, Guid userId, Guid tenantId);

    /// <summary>
    /// Gets count of unread notifications for a user
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Marks a single notification as read
    /// </summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, Guid tenantId);

    /// <summary>
    /// Marks all notifications as read for a user
    /// </summary>
    Task<int> MarkAllAsReadAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Deletes a notification
    /// </summary>
    Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId, Guid tenantId);

    /// <summary>
    /// Creates a new notification record
    /// </summary>
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto createNotificationDto, Guid createdBy, Guid tenantId);

    #endregion

    #region Push Subscriptions - Device & Browser Management

    /// <summary>
    /// Subscribes user to push notifications from a device/browser
    /// </summary>
    Task SubscribeToPushNotificationsAsync(Guid userId, PushSubscriptionDto subscriptionDto);

    /// <summary>
    /// Unsubscribes user from push notifications on a specific device/browser
    /// </summary>
    Task UnsubscribeFromPushNotificationsAsync(Guid userId, PushSubscriptionDto subscriptionDto);

    #endregion

    #region User Preferences

    /// <summary>
    /// Retrieves user's notification preferences and settings
    /// </summary>
    Task<NotificationPreferencesDto> GetPreferencesAsync(Guid userId);

    /// <summary>
    /// Updates user's notification preferences (opt-in/opt-out channels, frequency, etc.)
    /// </summary>
    Task<NotificationPreferencesDto> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesDto preferencesDto);

    #endregion

    #region Statistics & Reporting

    /// <summary>
    /// Gets notification statistics for tenant (sent, delivered, failed counts)
    /// </summary>
    Task<NotificationStatisticsDto> GetStatisticsAsync(Guid tenantId, string period = "last-30-days", bool isSuperAdmin = false);

    #endregion

    #region Notification Templates - Reusable Message Templates

    /// <summary>
    /// Retrieves all notification templates, optionally filtered by type
    /// </summary>
    Task<List<ErpSystem.Core.DTOs.Notifications.NotificationTemplateDto>> GetNotificationTemplatesAsync(Guid tenantId, string? type = null);

    /// <summary>
    /// Creates a new notification template
    /// </summary>
    Task<ErpSystem.Core.DTOs.Notifications.NotificationTemplateDto> CreateNotificationTemplateAsync(
        ErpSystem.Core.DTOs.Notifications.CreateNotificationTemplateDto templateDto, Guid createdBy, Guid tenantId);

    /// <summary>
    /// Deletes a notification template
    /// </summary>
    Task<bool> DeleteNotificationTemplateAsync(Guid templateId, Guid tenantId);

    #endregion

    #region Email Campaigns - Bulk Messaging

    /// <summary>
    /// Creates an email campaign for bulk messaging
    /// </summary>
    Task<EmailCampaignDto> CreateEmailCampaignAsync(CreateEmailCampaignDto campaignDto, Guid createdBy, Guid tenantId);

    /// <summary>
    /// Retrieves a specific email campaign
    /// </summary>
    Task<EmailCampaignDto?> GetEmailCampaignAsync(Guid campaignId, Guid tenantId);

    /// <summary>
    /// Retrieves all email campaigns, optionally filtered by status
    /// </summary>
    Task<List<EmailCampaignDto>> GetEmailCampaignsAsync(Guid tenantId, string? status = null);

    /// <summary>
    /// Deletes an email campaign
    /// </summary>
    Task<bool> DeleteEmailCampaignAsync(Guid campaignId, Guid tenantId);

    /// <summary>
    /// Sends an email campaign to all subscribers
    /// </summary>
    Task<EmailCampaignDto?> SendEmailCampaignAsync(Guid campaignId, Guid tenantId);

    #endregion

    #region Real-time Delivery - SignalR/WebSocket Broadcasting

    /// <summary>
    /// Sends notification in real-time to specific users connected via WebSocket
    /// </summary>
    Task SendRealTimeNotificationAsync(NotificationDto notification, List<Guid> userIds);

    /// <summary>
    /// Broadcasts notification to all users with specific roles in a tenant
    /// </summary>
    Task BroadcastNotificationAsync(NotificationDto notification, Guid tenantId, List<string>? roles = null);

    #endregion

    #region Background Processing

    /// <summary>
    /// Processes pending notifications that were queued for delivery
    /// </summary>
    Task ProcessPendingNotificationsAsync();

    /// <summary>
    /// Cleans up expired or old notifications (configurable retention policy)
    /// </summary>
    Task CleanupExpiredNotificationsAsync();

    #endregion
}
