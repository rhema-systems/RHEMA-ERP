using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Maintenance;

/// <summary>
/// Represents a maintenance notification
/// </summary>
public class MaintenanceNotification : TenantEntity
{
    // Inherits Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, TenantId from TenantEntity

    /// <summary>
    /// Type of notification (OverdueMaintenance, MaintenanceDue, WorkOrderAssigned, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// The entity this notification refers to (WorkOrder, Asset, MaintenanceSchedule)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the entity this notification refers to
    /// </summary>
    [Required]
    public Guid EntityId { get; set; }

    /// <summary>
    /// User who should receive this notification
    /// </summary>
    [Required]
    public Guid RecipientId { get; set; }

    /// <summary>
    /// Alternative: Role that should receive this notification
    /// </summary>
    [MaxLength(100)]
    public string? RecipientRole { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (Low, Normal, High, Critical)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Current status (Pending, Sent, Read, Dismissed)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// When this notification should be sent
    /// </summary>
    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this notification was actually sent
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// When this notification was read by recipient
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// When this notification was dismissed
    /// </summary>
    public DateTime? DismissedAt { get; set; }

    /// <summary>
    /// Number of times we've attempted to send this notification
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Last error message if sending failed
    /// </summary>
    [MaxLength(500)]
    public string? LastError { get; set; }

    /// <summary>
    /// Additional data for the notification (JSON)
    /// </summary>
    public string? AdditionalData { get; set; }

    /// <summary>
    /// Action URL for the notification
    /// </summary>
    [MaxLength(500)]
    public string? ActionUrl { get; set; }
}

/// <summary>
/// Template for generating notifications
/// </summary>
public class MaintenanceNotificationTemplate : TenantEntity
{
    // Inherits Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, TenantId from TenantEntity

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of notification this template is for
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Template for the notification title (supports placeholders)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string TitleTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Template for the notification message (supports placeholders)
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string MessageTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Default priority for notifications using this template
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string DefaultPriority { get; set; } = "Normal";

    /// <summary>
    /// How many minutes before the due date to send notification
    /// </summary>
    public int LeadTimeMinutes { get; set; } = 1440; // 24 hours default

    /// <summary>
    /// Whether this template is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Roles that should receive notifications from this template
    /// </summary>
    [MaxLength(500)]
    public string? DefaultRoles { get; set; }

    /// <summary>
    /// Delivery methods (Email, SMS, Push, InApp)
    /// </summary>
    [MaxLength(200)]
    public string DeliveryMethods { get; set; } = "InApp";
}

/// <summary>
/// Tracks escalation rules for overdue maintenance
/// </summary>
public class MaintenanceEscalationRule : TenantEntity
{
    // Inherits Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, TenantId from TenantEntity

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Entity type this rule applies to (WorkOrder, MaintenanceSchedule)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Condition for triggering escalation (JSON expression)
    /// </summary>
    [Required]
    public string TriggerCondition { get; set; } = string.Empty;

    /// <summary>
    /// How many hours overdue before escalation triggers
    /// </summary>
    public int HoursOverdue { get; set; } = 24;

    /// <summary>
    /// Priority level that triggers this escalation
    /// </summary>
    [MaxLength(20)]
    public string? TriggerPriority { get; set; }

    /// <summary>
    /// Role to escalate to
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EscalationRole { get; set; } = string.Empty;

    /// <summary>
    /// Specific user to escalate to (alternative to role)
    /// </summary>
    public Guid? EscalationUserId { get; set; }

    /// <summary>
    /// Notification template to use for escalation
    /// </summary>
    public Guid? NotificationTemplateId { get; set; }

    /// <summary>
    /// Whether this rule is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Order of execution if multiple rules apply
    /// </summary>
    public int Priority { get; set; } = 0;

    // Navigation properties
    [ForeignKey("NotificationTemplateId")]
    public virtual MaintenanceNotificationTemplate? NotificationTemplate { get; set; }
}

/// <summary>
/// Tracks when escalations have been triggered to avoid duplicates
/// </summary>
public class MaintenanceEscalationHistory
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid EscalationRuleId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public Guid EntityId { get; set; }

    [Required]
    public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;

    public Guid? EscalatedToUserId { get; set; }

    [MaxLength(100)]
    public string? EscalatedToRole { get; set; }

    public Guid? NotificationId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public Guid TenantId { get; set; }

    // Navigation properties
    [ForeignKey("EscalationRuleId")]
    public virtual MaintenanceEscalationRule EscalationRule { get; set; } = null!;

    [ForeignKey("NotificationId")]
    public virtual MaintenanceNotification? Notification { get; set; }
}