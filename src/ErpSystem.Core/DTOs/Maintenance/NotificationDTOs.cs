using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

#region Enums

/// <summary>
/// Notification type enumeration
/// </summary>
public enum NotificationType
{
    ScheduleReminder,
    OverdueAlert,
    Assignment,
    StatusChange,
    Completion,
    AssetCritical,
    WarrantyExpiration,
    SafetyViolation,
    ComplianceReminder,
    MaintenanceDue,
    InspectionRequired,
    PartRequest,
    WorkOrderCreated,
    SystemAlert
}

/// <summary>
/// Notification priority enumeration
/// </summary>
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}

#endregion

#region Notification DTOs

/// <summary>
/// Maintenance notification DTO
/// </summary>
public class MaintenanceNotificationDto
{
    public Guid Id { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid RecipientId { get; set; }
    public string? RecipientRole { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public string? AdditionalData { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedDate { get; set; }

    // Calculated properties
    public bool IsRead { get; set; }
    public bool IsOverdue { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;

    // Additional properties for service compatibility
    public NotificationType Type { get; set; }
    public string RelatedEntityType { get; set; } = string.Empty;
    public Guid RelatedEntityId { get; set; }
    public List<NotificationRecipientDto> Recipients { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// DTO for creating maintenance notifications
/// </summary>
public class CreateMaintenanceNotificationDto
{
    [Required]
    [StringLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public Guid EntityId { get; set; }

    [Required]
    public Guid RecipientId { get; set; }

    [StringLength(100)]
    public string? RecipientRole { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(20)]
    public string Priority { get; set; } = "Normal";

    public DateTime? ScheduledFor { get; set; }

    public string? AdditionalData { get; set; }

    [StringLength(500)]
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Tenant ID - used for background service context (when current user is null).
    /// If not provided, will use current user's tenant from context.
    /// For background/scheduled operations, set this explicitly to the DEFAULT or target tenant.
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// DTO for bulk creating notifications
/// </summary>
public class BulkCreateNotificationDto
{
    [Required]
    [StringLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public List<Guid> EntityIds { get; set; } = new();

    public List<Guid>? RecipientIds { get; set; }

    [StringLength(100)]
    public string? RecipientRole { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(20)]
    public string Priority { get; set; } = "Normal";

    public DateTime? ScheduledFor { get; set; }
}

/// <summary>
/// DTO for updating notification status
/// </summary>
public class UpdateNotificationStatusDto
{
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty; // Read, Dismissed
}

/// <summary>
/// Notification recipient DTO
/// </summary>
public class NotificationRecipientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string DeliveryMethod { get; set; } = string.Empty;
}

#endregion

#region Notification Template DTOs

/// <summary>
/// Notification template DTO
/// </summary>
public class MaintenanceNotificationTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string DefaultPriority { get; set; } = string.Empty;
    public int LeadTimeMinutes { get; set; }
    public bool IsActive { get; set; }
    public string? DefaultRoles { get; set; }
    public string DeliveryMethods { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? LastModifiedByName { get; set; }
}

/// <summary>
/// DTO for creating notification templates
/// </summary>
public class CreateNotificationTemplateDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(200)]
    public string TitleTemplate { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string MessageTemplate { get; set; } = string.Empty;

    [StringLength(20)]
    public string DefaultPriority { get; set; } = "Normal";

    public int LeadTimeMinutes { get; set; } = 1440;

    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? DefaultRoles { get; set; }

    [StringLength(200)]
    public string DeliveryMethods { get; set; } = "InApp";
}

/// <summary>
/// DTO for updating notification templates
/// </summary>
public class UpdateNotificationTemplateDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(200)]
    public string TitleTemplate { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string MessageTemplate { get; set; } = string.Empty;

    [StringLength(20)]
    public string DefaultPriority { get; set; } = "Normal";

    public int LeadTimeMinutes { get; set; }

    public bool IsActive { get; set; }

    [StringLength(500)]
    public string? DefaultRoles { get; set; }

    [StringLength(200)]
    public string DeliveryMethods { get; set; } = "InApp";
}

#endregion

#region Escalation Rule DTOs

/// <summary>
/// Escalation rule DTO
/// </summary>
public class MaintenanceEscalationRuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string TriggerCondition { get; set; } = string.Empty;
    public int HoursOverdue { get; set; }
    public string? TriggerPriority { get; set; }
    public string EscalationRole { get; set; } = string.Empty;
    public Guid? EscalationUserId { get; set; }
    public Guid? NotificationTemplateId { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }

    // Navigation properties
    public string? EscalationUserName { get; set; }
    public string? NotificationTemplateName { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? LastModifiedByName { get; set; }
}

/// <summary>
/// DTO for creating escalation rules
/// </summary>
public class CreateEscalationRuleDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public string TriggerCondition { get; set; } = string.Empty;

    public int HoursOverdue { get; set; } = 24;

    [StringLength(20)]
    public string? TriggerPriority { get; set; }

    [Required]
    [StringLength(100)]
    public string EscalationRole { get; set; } = string.Empty;

    public Guid? EscalationUserId { get; set; }

    public Guid? NotificationTemplateId { get; set; }

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 0;
}

/// <summary>
/// DTO for updating escalation rules
/// </summary>
public class UpdateEscalationRuleDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string TriggerCondition { get; set; } = string.Empty;

    public int HoursOverdue { get; set; }

    [StringLength(20)]
    public string? TriggerPriority { get; set; }

    [Required]
    [StringLength(100)]
    public string EscalationRole { get; set; } = string.Empty;

    public Guid? EscalationUserId { get; set; }

    public Guid? NotificationTemplateId { get; set; }

    public bool IsActive { get; set; }

    public int Priority { get; set; }
}

#endregion

#region Filter and Summary DTOs

/// <summary>
/// Filter DTO for notifications
/// </summary>
public class NotificationFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? NotificationType { get; set; }
    public string? EntityType { get; set; }
    public Guid? RecipientId { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsRead { get; set; }
    public bool? IsOverdue { get; set; }
    public string SortBy { get; set; } = "CreatedDate";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Notification summary DTO
/// </summary>
public class NotificationSummaryDto
{
    public int TotalNotifications { get; set; }
    public int UnreadNotifications { get; set; }
    public int PendingNotifications { get; set; }
    public int OverdueNotifications { get; set; }
    public int CriticalNotifications { get; set; }
    public int FailedNotifications { get; set; }
    public Dictionary<string, int> NotificationsByType { get; set; } = new();
    public Dictionary<string, int> NotificationsByPriority { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Escalation history DTO
/// </summary>
public class EscalationHistoryDto
{
    public Guid Id { get; set; }
    public string EscalationRuleName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public DateTime EscalatedAt { get; set; }
    public string? EscalatedToUserName { get; set; }
    public string? EscalatedToRole { get; set; }
    public string? Notes { get; set; }
}

#endregion
