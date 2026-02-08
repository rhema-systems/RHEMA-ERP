using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

[Table("EmailCampaignRecipients")]
public class EmailCampaignRecipient : TenantEntity
{
    [Required]
    public Guid EmailCampaignId { get; set; }
    public virtual EmailCampaign EmailCampaign { get; set; } = null!;

    public Guid? UserId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// UserId, Email, Role
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Source { get; set; } = "Email";

    [MaxLength(100)]
    public string? SourceRole { get; set; }

    /// <summary>
    /// Pending, Sent, Failed
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime? SentAt { get; set; }

    public int AttemptCount { get; set; } = 0;
    public string? LastError { get; set; }

    public Guid? NotificationId { get; set; }
}

