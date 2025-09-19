using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

public class EmailSettings : BaseEntity
{
    [Required]
    [StringLength(255)]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [StringLength(255)]
    public string SmtpUsername { get; set; } = string.Empty;

    [StringLength(255)]
    public string SmtpPassword { get; set; } = string.Empty;

    public bool UseTLS { get; set; } = true;

    [Required]
    [StringLength(255)]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string FromName { get; set; } = string.Empty;

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

public class PasswordPolicy : BaseEntity
{
    [Range(1, 128)]
    public int MinLength { get; set; } = 8;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireDigits { get; set; } = true;

    public bool RequireSpecialChars { get; set; } = true;

    [Range(0, 365)]
    public int? MaxAge { get; set; } = 90; // Days

    [Range(0, 50)]
    public int? PreventReuse { get; set; } = 5; // Number of previous passwords to prevent reuse

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

public class SystemSettings : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsEncrypted { get; set; } = false;

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}