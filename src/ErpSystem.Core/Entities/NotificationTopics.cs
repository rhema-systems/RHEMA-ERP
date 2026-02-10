using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Admin-configurable notification topic ("group") that can be triggered by any module.
/// Modules publish events by TopicKey; the topic controls channels, templates and recipients.
/// </summary>
[Table("NotificationTopics")]
public class NotificationTopic : TenantEntity
{
    [Required]
    [MaxLength(120)]
    public string Key { get; set; } = string.Empty; // e.g. "RFQ.QuoteSubmitted.Internal"

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Optional: entity type label for organization in the UI (e.g. "Rfq", "WorkOrder").
    /// This does not enforce anything at runtime.
    /// </summary>
    [MaxLength(100)]
    public string? EntityType { get; set; }

    /// <summary>
    /// System-seeded topic. System topics cannot be deleted from the admin UI/API.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Required topic. Required topics cannot be deactivated and must have at least one channel enabled.
    /// </summary>
    public bool IsRequired { get; set; } = false;

    public bool IsActive { get; set; } = true;

    // Channels
    public bool EnableInApp { get; set; } = true;
    public bool EnableEmail { get; set; } = false;

    // In-app templates (tokenized using {{token}} from event data)
    [MaxLength(200)]
    public string? InAppTitleTemplate { get; set; }

    public string? InAppBodyTemplate { get; set; }

    // Email template uses existing EmailTemplates (Module="Notifications") to reuse UI/editor
    public Guid? EmailTemplateId { get; set; }

    [ForeignKey(nameof(EmailTemplateId))]
    public virtual EmailTemplate? EmailTemplate { get; set; }

    [MaxLength(500)]
    public string? ActionUrlTemplate { get; set; }

    public virtual ICollection<NotificationTopicRecipient> Recipients { get; set; } = new List<NotificationTopicRecipient>();
}

/// <summary>
/// Recipient rule for a notification topic.
/// </summary>
[Table("NotificationTopicRecipients")]
public class NotificationTopicRecipient : TenantEntity
{
    [Required]
    public Guid TopicId { get; set; }

    [ForeignKey(nameof(TopicId))]
    public virtual NotificationTopic Topic { get; set; } = null!;

    /// <summary>
    /// Supported kinds: "User", "Role", "UserFromData", "UserFromEmployeeIdData", "UsersFromData",
    /// "RoleFromData", "BusinessPartner", "BusinessPartnerFromData", "EmailFromData", "DepartmentType".
    /// </summary>
    [Required]
    [MaxLength(40)]
    public string RecipientKind { get; set; } = "Role";

    /// <summary>
    /// Meaning depends on RecipientKind:
    /// - User: userId (Guid)
    /// - Role: role name
    /// - BusinessPartnerFromData: data key holding a businessPartnerId (Guid)
    /// - EmailFromData: data key holding email(s) (string)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string RecipientValue { get; set; } = string.Empty;

    /// <summary>
    /// System recipient rule seeded by the platform. System recipient rules are protected from edits/removal.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    public bool SendInApp { get; set; } = true;
    public bool SendEmail { get; set; } = false;
}

