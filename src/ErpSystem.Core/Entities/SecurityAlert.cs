using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities;

public class SecurityAlert : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty; // 'critical', 'warning', 'info'

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool Dismissed { get; set; } = false;
    public DateTime? DismissedAt { get; set; }
    public string? DismissedBy { get; set; }

    [Range(1, 10)]
    public int Severity { get; set; } = 1;

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty; // 'authentication', 'authorization', 'data', 'system', 'policy'

    [Required]
    [StringLength(100)]
    public string Source { get; set; } = string.Empty;

    [StringLength(100)]
    public string? AffectedUser { get; set; }

    [StringLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    // JSON metadata as text
    [StringLength(2048)]
    public string? Metadata { get; set; }

    // Navigation properties
    public Guid? AffectedUserId { get; set; }
    public virtual ApplicationUser? AffectedUserEntity { get; set; }
}
