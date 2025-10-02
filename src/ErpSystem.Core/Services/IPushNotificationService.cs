using ErpSystem.Core.DTOs.Notifications;

namespace ErpSystem.Core.Services
{
    public interface IPushNotificationService
    {
        // Web Push integration for PWA
        Task<bool> SendWebPushNotificationAsync(
            PushSubscriptionDto subscription, 
            object payload, 
            string? vapidSubject = null);
        
        Task<List<bool>> SendWebPushToMultipleAsync(
            List<PushSubscriptionDto> subscriptions, 
            object payload, 
            string? vapidSubject = null);
        
        Task<bool> SendToUserAsync(Guid userId, object payload);
        Task<bool> SendToUsersAsync(List<Guid> userIds, object payload);
        Task<bool> SendToRoleAsync(string role, Guid tenantId, object payload);
        Task<bool> BroadcastToTenantAsync(Guid tenantId, object payload);
        
        // Subscription management
        Task<bool> SaveSubscriptionAsync(Guid userId, PushSubscriptionDto subscription);
        Task<bool> RemoveSubscriptionAsync(Guid userId, string endpoint);
        Task<List<PushSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId);
        Task<bool> ValidateSubscriptionAsync(PushSubscriptionDto subscription);
        
        // VAPID key management
        Task<VapidKeysDto> GenerateVapidKeysAsync();
        Task<VapidKeysDto> GetVapidKeysAsync();
        Task<string> GetPublicVapidKeyAsync();
        
        // Push notification templates
        Task<object> CreatePushPayloadAsync(
            string title, 
            string body, 
            string? icon = null, 
            string? badge = null,
            string? image = null,
            Dictionary<string, object>? data = null,
            List<NotificationActionDto>? actions = null);
        
        // Delivery tracking
        Task<bool> TrackDeliveryAsync(string notificationId, string status, string? errorMessage = null);
        Task<NotificationDeliveryStatsDto> GetDeliveryStatsAsync(Guid tenantId, DateTime? from = null, DateTime? to = null);
        
        // Testing and diagnostics
        Task<bool> TestPushNotificationAsync(Guid userId, string message);
        Task<PushNotificationHealthDto> GetHealthStatusAsync();
        Task<List<FailedPushNotificationDto>> GetFailedNotificationsAsync(int maxResults = 100);
    }
    
    public class VapidKeysDto
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
    }
    
    public class NotificationDeliveryStatsDto
    {
        public int TotalSent { get; set; }
        public int TotalDelivered { get; set; }
        public int TotalFailed { get; set; }
        public double DeliveryRate { get; set; }
        public List<DeliveryStatsByDateDto>? DailyStats { get; set; }
        public List<FailureReasonDto>? FailureReasons { get; set; }
    }
    
    public class DeliveryStatsByDateDto
    {
        public DateTime Date { get; set; }
        public int Sent { get; set; }
        public int Delivered { get; set; }
        public int Failed { get; set; }
    }
    
    public class FailureReasonDto
    {
        public string Reason { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }
    
    public class PushNotificationHealthDto
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastCheck { get; set; }
        public List<string>? Issues { get; set; }
        public Dictionary<string, object>? Metrics { get; set; }
    }
    
    public class FailedPushNotificationDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRetryAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
    }
}