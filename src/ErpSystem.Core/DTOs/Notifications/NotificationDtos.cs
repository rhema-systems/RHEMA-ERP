namespace ErpSystem.Core.DTOs.Notifications
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "info"; // info, warning, error, success
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public string? ActionUrl { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public string? IconUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? EntityType { get; set; }
        public Guid? EntityId { get; set; }
    }

    public class CreateNotificationDto
    {
        public Guid RecipientId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Priority { get; set; } = "Normal"; // Low, Normal, High, Critical
        public string EntityType { get; set; } = string.Empty; // e.g., "JobCard", "WorkOrder", "PurchaseOrder"
        public Guid EntityId { get; set; } // ID of the entity that triggered this notification
        public string? ActionUrl { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNext => Page < TotalPages;
        public bool HasPrevious => Page > 1;
    }

    public class SendPushNotificationDto
    {
        public List<Guid>? UserIds { get; set; }
        public List<string>? UserRoles { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Badge { get; set; }
        public string? Image { get; set; }
        public string? Tag { get; set; }
        public Dictionary<string, object>? Data { get; set; }
        public List<NotificationActionDto>? Actions { get; set; }
        public bool RequireInteraction { get; set; } = false;
        public bool Silent { get; set; } = false;
        public int[]? Vibrate { get; set; }
        public DateTime? ScheduledFor { get; set; }
    }

    public class NotificationActionDto
    {
        public string Action { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }

    public class NotificationPreferencesDto
    {
        public Guid UserId { get; set; }
        public bool EnablePushNotifications { get; set; } = true;
        public bool EnableEmailNotifications { get; set; } = true;
        public bool EnableSmsNotifications { get; set; } = false;
        public Dictionary<string, bool> TypePreferences { get; set; } = new();
        public Dictionary<string, string> DeliveryTimes { get; set; } = new();
        public string TimeZone { get; set; } = "UTC";
        public string Language { get; set; } = "en";
    }

    public class UpdateNotificationPreferencesDto
    {
        public bool? EnablePushNotifications { get; set; }
        public bool? EnableEmailNotifications { get; set; }
        public bool? EnableSmsNotifications { get; set; }
        public Dictionary<string, bool>? TypePreferences { get; set; }
        public Dictionary<string, string>? DeliveryTimes { get; set; }
        public string? TimeZone { get; set; }
        public string? Language { get; set; }
    }

    public class PushSubscriptionDto
    {
        public string Endpoint { get; set; } = string.Empty;
        public PushSubscriptionKeysDto Keys { get; set; } = new();
        public string? UserAgent { get; set; }
        public DateTime? ExpirationTime { get; set; }
    }

    public class PushSubscriptionKeysDto
    {
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }

    public class NotificationStatisticsDto
    {
        public int TotalNotifications { get; set; }
        public int UnreadNotifications { get; set; }
        public int NotificationsSentToday { get; set; }
        public int PushNotificationsSent { get; set; }
        public int EmailNotificationsSent { get; set; }
        public double DeliveryRate { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
        /// <summary>
        /// Average delivery time in seconds for notifications that have a SentAt timestamp.
        /// </summary>
        public double AverageDeliveryTimeSeconds { get; set; }
        public List<NotificationTypeStatsDto>? TypeStatistics { get; set; }
        public List<NotificationTrendDto>? Trends { get; set; }
        public List<TopNotificationDto>? TopPerformingNotifications { get; set; }
    }

    public class NotificationTypeStatsDto
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Delivered { get; set; }
        public int Opened { get; set; }
        public int Clicked { get; set; }
        public double DeliveryRate { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
    }

    public class NotificationTrendDto
    {
        public DateTime Date { get; set; }
        public int NotificationsSent { get; set; }
        public int NotificationsDelivered { get; set; }
        public int NotificationsOpened { get; set; }
        public int NotificationsClicked { get; set; }
    }

    public class TopNotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Sent { get; set; }
        public int Delivered { get; set; }
        public int Opened { get; set; }
        public int Clicked { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class CreateEmailCampaignDto
    {
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public string? FromName { get; set; }
        public string? FromEmail { get; set; }
        public string? ReplyTo { get; set; }
        public List<Guid>? RecipientUserIds { get; set; }
        public List<string>? RecipientEmails { get; set; }
        public List<string>? RecipientRoles { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
        public DateTime? ScheduledFor { get; set; }
        public bool IsTemplate { get; set; } = false;
        public string? TemplateData { get; set; }
    }

    public class EmailCampaignDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public string? FromName { get; set; }
        public string? FromEmail { get; set; }
        public string? ReplyTo { get; set; }
        public int TotalRecipients { get; set; }
        public int SentCount { get; set; }
        public int DeliveredCount { get; set; }
        public int OpenedCount { get; set; }
        public int ClickedCount { get; set; }
        public int BouncedCount { get; set; }
        public int UnsubscribedCount { get; set; }
        public string Status { get; set; } = string.Empty; // draft, scheduled, sending, sent, cancelled
        public DateTime CreatedAt { get; set; }
        public DateTime? ScheduledFor { get; set; }
        public DateTime? SentAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public Dictionary<string, string>? Tags { get; set; }
        public EmailCampaignStatsDto? Statistics { get; set; }
    }

    public class EmailCampaignStatsDto
    {
        public double DeliveryRate { get; set; }
        public double OpenRate { get; set; }
        public double ClickRate { get; set; }
        public double BounceRate { get; set; }
        public double UnsubscribeRate { get; set; }
        public TimeSpan? AvgTimeToOpen { get; set; }
        public List<EmailClickDto>? TopClicks { get; set; }
        public List<EmailDeviceStatsDto>? DeviceStats { get; set; }
        public List<EmailLocationStatsDto>? LocationStats { get; set; }
    }

    public class EmailClickDto
    {
        public string Url { get; set; } = string.Empty;
        public int Clicks { get; set; }
        public int UniqueClicks { get; set; }
    }

    public class EmailDeviceStatsDto
    {
        public string DeviceType { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class EmailLocationStatsDto
    {
        public string Country { get; set; } = string.Empty;
        public string? Region { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class NotificationTemplateDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlTemplate { get; set; } = string.Empty;
        public string? TextTemplate { get; set; }
        public List<string>? Variables { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsed { get; set; }
        public int UsageCount { get; set; }
    }

    public class CreateNotificationTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlTemplate { get; set; } = string.Empty;
        public string? TextTemplate { get; set; }
        public List<string>? Variables { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class NotificationDeliveryStatusDto
    {
        public Guid NotificationId { get; set; }
        public Guid UserId { get; set; }
        public string DeliveryMethod { get; set; } = string.Empty; // push, email, sms
        public string Status { get; set; } = string.Empty; // pending, sent, delivered, failed, opened, clicked
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? OpenedAt { get; set; }
        public DateTime? ClickedAt { get; set; }
        public string? FailureReason { get; set; }
        public int RetryCount { get; set; }
        public DateTime? NextRetryAt { get; set; }
    }
}
