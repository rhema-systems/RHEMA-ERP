using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities;

public class ThreatDetection : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string ThreatType { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ThreatSeverity Severity { get; set; }

    [Required]
    public ThreatStatus Status { get; set; } = ThreatStatus.New;

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(200)]
    public string? UserAgent { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    public Guid? AffectedUserId { get; set; }
    public virtual ApplicationUser? AffectedUser { get; set; }

    [StringLength(100)]
    public string? AffectedUserName { get; set; }

    [StringLength(2048)]
    public string? Metadata { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    [StringLength(1000)]
    public string? Resolution { get; set; }

    [StringLength(100)]
    public string? ResolvedBy { get; set; }

    public int RiskScore { get; set; }

    public bool IsBlocked { get; set; } = false;

    // Navigation properties for threat indicators
    public virtual ICollection<ThreatIndicator> Indicators { get; set; } = new List<ThreatIndicator>();

    // Tenant association
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

public class ThreatIndicator : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string IndicatorType { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Value { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int Confidence { get; set; } // 0-100

    public bool IsActive { get; set; } = true;

    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public int HitCount { get; set; } = 1;

    // Foreign key to ThreatDetection
    public Guid ThreatDetectionId { get; set; }
    public virtual ThreatDetection ThreatDetection { get; set; } = null!;

    // Tenant association
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}