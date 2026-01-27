using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities;

public class SecurityPolicy : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public SecurityPolicyType Type { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public bool IsEnforced { get; set; } = false;

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [Required]
    [StringLength(5000)]
    public string PolicyRules { get; set; } = string.Empty; // JSON configuration

    [StringLength(2000)]
    public string? Exceptions { get; set; } // JSON list of exceptions

    public int Priority { get; set; } = 0; // Higher numbers = higher priority

    public DateTime LastEvaluated { get; set; } = DateTime.UtcNow;

    public int ViolationCount { get; set; } = 0;

    public DateTime? LastViolation { get; set; }

    [StringLength(1000)]
    public string? NotificationSettings { get; set; } // JSON configuration

    public bool RequiresApproval { get; set; } = false;

    [StringLength(100)]
    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [StringLength(500)]
    public string? ApprovalNotes { get; set; }

    // Policy violations tracking
    public virtual ICollection<SecurityPolicyViolation> Violations { get; set; } = new List<SecurityPolicyViolation>();

    // Tenant association
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

public class SecurityPolicyViolation : BaseEntity
{
    public Guid SecurityPolicyId { get; set; }
    public virtual SecurityPolicy SecurityPolicy { get; set; } = null!;

    public Guid? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }

    [StringLength(100)]
    public string? UserName { get; set; }

    [Required]
    [StringLength(100)]
    public string ViolationType { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public SecurityPolicyViolationSeverity Severity { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(200)]
    public string? UserAgent { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(2048)]
    public string? Metadata { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public bool IsResolved { get; set; } = false;

    public DateTime? ResolvedAt { get; set; }

    [StringLength(100)]
    public string? ResolvedBy { get; set; }

    [StringLength(1000)]
    public string? Resolution { get; set; }

    public bool ActionTaken { get; set; } = false;

    [StringLength(500)]
    public string? ActionDescription { get; set; }

    // Tenant association
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}
