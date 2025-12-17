using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

public class RefreshToken : BaseEntity
{
    [Required]
    [StringLength(255)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Nullable tenant ID - null for secure flow tokens without tenant context
    /// </summary>
    public Guid? TenantId { get; set; }

    [Required]
    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    public DateTime? RevokedAt { get; set; }

    public Guid? RevokedBy { get; set; }

    [StringLength(200)]
    public string? RevocationReason { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public int UsageCount { get; set; } = 0;

    public int MaxUsageCount { get; set; } = 0; // 0 = unlimited

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    [StringLength(100)]
    public string? DeviceId { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Tenant? Tenant { get; set; }
}
