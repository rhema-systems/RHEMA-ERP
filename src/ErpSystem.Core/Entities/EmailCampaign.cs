using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

[Table("EmailCampaigns")]
public class EmailCampaign : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string HtmlContent { get; set; } = string.Empty;

    public string? TextContent { get; set; }

    [MaxLength(200)]
    public string? FromName { get; set; }

    [MaxLength(200)]
    public string? FromEmail { get; set; }

    [MaxLength(200)]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// draft, scheduled, sending, sent, failed, cancelled
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// JSON key/value tags for filtering (e.g. {"source":"admin","module":"procurement"}).
    /// </summary>
    public string? TagsJson { get; set; }

    public bool IsTemplate { get; set; } = false;
    public string? TemplateData { get; set; }

    public virtual ICollection<EmailCampaignRecipient> Recipients { get; set; } = new List<EmailCampaignRecipient>();
}

