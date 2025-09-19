using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

public class AuditLog : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(255)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Resource { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ResourceId { get; set; }

    public string? OldValues { get; set; } // JSON string

    public string? NewValues { get; set; } // JSON string

    [Required]
    [StringLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid TenantId { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}

public class SecurityLog : BaseEntity
{
    public Guid? UserId { get; set; }

    [StringLength(255)]
    public string? Username { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [StringLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public bool Success { get; set; }

    [StringLength(1000)]
    public string? Details { get; set; }

    [StringLength(255)]
    public string? FailureReason { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid TenantId { get; set; }

    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

public enum AuditAction
{
    Create,
    Read,
    Update,
    Delete,
    Login,
    Logout,
    PasswordChange,
    RoleChange,
    SettingChange,
    Export,
    Import
}

public enum SecurityAction
{
    LoginSuccess,
    LoginFailure,
    LogoutSuccess,
    PasswordChangeSuccess,
    PasswordChangeFailure,
    AccountLocked,
    AccountUnlocked,
    TwoFactorEnabled,
    TwoFactorDisabled,
    SuspiciousActivity,
    BruteForceAttempt
}