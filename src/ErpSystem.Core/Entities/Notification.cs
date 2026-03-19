using ErpSystem.Shared;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Generic notification entity for system-wide use.
/// Stores notifications across all modules (maintenance, HR, procurement, etc.)
/// </summary>
public class Notification : TenantEntity
{
    /// <summary>
    /// Unique identifier for the notification
    /// </summary>
    public new Guid Id { get; set; }

    /// <summary>
    /// Type of notification (e.g., "JobCardSubmitted", "GeneralAlert", etc.)
    /// </summary>
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// Title/subject of the notification
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Message body/content
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Priority level: Low, Normal, High, Critical
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Current status: Pending, Sent, Failed, Dismissed, Archived
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// ID of the user receiving this notification
    /// </summary>
    public Guid RecipientId { get; set; }

    /// <summary>
    /// Whether the notification has been read by the recipient
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Timestamp when the notification was read
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// When the notification was marked as dismissed
    /// </summary>
    public DateTime? DismissedAt { get; set; }

    /// <summary>
    /// Scheduled delivery time for the notification
    /// </summary>
    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the notification was actually sent
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Number of delivery attempts made
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Last error message if delivery failed
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Entity type that triggered this notification (e.g., "JobCard", "WorkOrder", etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Entity ID that triggered this notification
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? AdditionalData { get; set; }

    /// <summary>
    /// URL to navigate to when notification is clicked
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Delivery methods used: Email, SMS, Push, InApp
    /// </summary>
    public string? DeliveryMethods { get; set; } = "InApp";

    /// <summary>
    /// Email address this was sent to (for email delivery)
    /// </summary>
    public string? EmailAddress { get; set; }

    /// <summary>
    /// Phone number this was sent to (for SMS delivery)
    /// </summary>
    public string? PhoneNumber { get; set; }
}
