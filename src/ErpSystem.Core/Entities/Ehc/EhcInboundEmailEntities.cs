using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Ehc;

[Table("EhcInboundEmailChannels")]
public class EhcInboundEmailChannel : TenantEntity
{
    [Required]
    [StringLength(320)]
    public string MailboxAddress { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FolderName { get; set; } = "Inbox";

    public bool IsEnabled { get; set; } = true;

    public bool RequireKnownSender { get; set; } = true;

    public bool AutoProvisionUnknownSenders { get; set; } = false;

    public Guid? DefaultCategoryId { get; set; }

    public EhcTicketType DefaultTicketType { get; set; } = EhcTicketType.Helpdesk;

    public EhcTicketPriority DefaultPriority { get; set; } = EhcTicketPriority.Medium;

    [Column(TypeName = "nvarchar(max)")]
    public string? GraphDeltaLink { get; set; }

    public bool UseGraphWebhook { get; set; } = false;

    [StringLength(200)]
    public string? GraphSubscriptionId { get; set; }

    public DateTime? GraphSubscriptionExpiresAtUtc { get; set; }

    [StringLength(200)]
    public string? GraphClientState { get; set; }

    public DateTime? LastSyncedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public DateTime? LastSuccessAtUtc { get; set; }

    public int LastProcessedMessageCount { get; set; } = 0;

    public int ConsecutiveFailureCount { get; set; } = 0;

    public DateTime? LastWebhookReceivedAtUtc { get; set; }

    [StringLength(2000)]
    public string? LastError { get; set; }
}

[Table("EhcInboundEmailMessages")]
public class EhcInboundEmailMessage : TenantEntity
{
    [Required]
    public Guid ChannelId { get; set; }

    [ForeignKey(nameof(ChannelId))]
    public virtual EhcInboundEmailChannel Channel { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string GraphMessageId { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string InternetMessageId { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ConversationId { get; set; }

    [Required]
    public Guid TicketId { get; set; }

    [StringLength(320)]
    public string? FromAddress { get; set; }

    [StringLength(500)]
    public string? Subject { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
}

[Table("EhcInboundEmailWebhookQueueItems")]
public class EhcInboundEmailWebhookQueueItem : TenantEntity
{
    [Required]
    public Guid ChannelId { get; set; }

    [ForeignKey(nameof(ChannelId))]
    public virtual EhcInboundEmailChannel Channel { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string GraphMessageId { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }

    [StringLength(2000)]
    public string? LastError { get; set; }
}
