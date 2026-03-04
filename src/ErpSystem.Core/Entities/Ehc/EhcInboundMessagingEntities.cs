using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Ehc;

public enum EhcInboundMessagingProvider
{
    Twilio = 1
}

[Table("EhcInboundMessagingChannels")]
public class EhcInboundMessagingChannel : TenantEntity
{
    public EhcInboundMessagingProvider Provider { get; set; } = EhcInboundMessagingProvider.Twilio;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// SMS or WhatsApp.
    /// </summary>
    public EhcTicketSource Source { get; set; } = EhcTicketSource.Sms;

    /// <summary>
    /// Destination address / phone number (e.g. +15551234567).
    /// For WhatsApp, store the number without the "whatsapp:" prefix.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string ToAddress { get; set; } = string.Empty;

    public bool RequireKnownSender { get; set; } = true;

    public bool AutoProvisionUnknownSenders { get; set; } = false;

    public Guid? DefaultCategoryId { get; set; }

    public EhcTicketType DefaultTicketType { get; set; } = EhcTicketType.Helpdesk;

    public EhcTicketPriority DefaultPriority { get; set; } = EhcTicketPriority.Medium;
}

[Table("EhcInboundMessagingMessages")]
public class EhcInboundMessagingMessage : TenantEntity
{
    [Required]
    public Guid ChannelId { get; set; }

    [ForeignKey(nameof(ChannelId))]
    public virtual EhcInboundMessagingChannel Channel { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string ProviderMessageId { get; set; } = string.Empty;

    [StringLength(64)]
    public string? FromAddress { get; set; }

    [StringLength(64)]
    public string? ToAddress { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Body { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid TicketId { get; set; }
}

