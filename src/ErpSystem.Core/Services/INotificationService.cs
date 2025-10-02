using ErpSystem.Core.DTOs.Notifications;

namespace ErpSystem.Core.Services
{
    public interface INotificationService
    {
        // Notification CRUD operations
        Task<PagedResult<NotificationDto>> GetNotificationsAsync(
            Guid userId, Guid tenantId, int page = 1, int pageSize = 20, 
            bool? unreadOnly = null, string? type = null, string? severity = null);
        
        Task<NotificationDto?> GetNotificationAsync(Guid notificationId, Guid userId, Guid tenantId);
        Task<int> GetUnreadCountAsync(Guid userId, Guid tenantId);
        Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, Guid tenantId);
        Task<int> MarkAllAsReadAsync(Guid userId, Guid tenantId);
        Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId, Guid tenantId);
        Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto createNotificationDto, Guid createdBy, Guid tenantId);

        // Push notification operations
        Task SendPushNotificationAsync(SendPushNotificationDto pushNotificationDto, Guid sentBy, Guid tenantId);
        Task SubscribeToPushNotificationsAsync(Guid userId, PushSubscriptionDto subscriptionDto);
        Task UnsubscribeFromPushNotificationsAsync(Guid userId, PushSubscriptionDto subscriptionDto);

        // Notification preferences
        Task<NotificationPreferencesDto> GetPreferencesAsync(Guid userId);
        Task<NotificationPreferencesDto> UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferencesDto preferencesDto);

        // Statistics and analytics
        Task<NotificationStatisticsDto> GetStatisticsAsync(Guid tenantId, string period = "last-30-days", bool isSuperAdmin = false);

        // Email campaign operations
        Task<EmailCampaignDto> CreateEmailCampaignAsync(CreateEmailCampaignDto campaignDto, Guid createdBy, Guid tenantId);
        Task<EmailCampaignDto?> GetEmailCampaignAsync(Guid campaignId, Guid tenantId);
        Task<List<EmailCampaignDto>> GetEmailCampaignsAsync(Guid tenantId, string? status = null);
        Task<bool> DeleteEmailCampaignAsync(Guid campaignId, Guid tenantId);
        Task<EmailCampaignDto?> SendEmailCampaignAsync(Guid campaignId, Guid tenantId);

        // Template operations
        Task<List<NotificationTemplateDto>> GetNotificationTemplatesAsync(Guid tenantId, string? type = null);
        Task<NotificationTemplateDto> CreateNotificationTemplateAsync(CreateNotificationTemplateDto templateDto, Guid createdBy, Guid tenantId);
        Task<bool> DeleteNotificationTemplateAsync(Guid templateId, Guid tenantId);

        // Real-time notification delivery
        Task SendRealTimeNotificationAsync(NotificationDto notification, List<Guid> userIds);
        Task BroadcastNotificationAsync(NotificationDto notification, Guid tenantId, List<string>? roles = null);
        
        // Background processing
        Task ProcessPendingNotificationsAsync();
        Task CleanupExpiredNotificationsAsync();
    }
}