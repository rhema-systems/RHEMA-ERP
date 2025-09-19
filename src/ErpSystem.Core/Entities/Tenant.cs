using System.ComponentModel.DataAnnotations;
using ErpSystem.Shared;

namespace ErpSystem.Core.Entities;

public class Tenant : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    [StringLength(200)]
    public string? Domain { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(200)]
    public string? ContactEmail { get; set; }

    [StringLength(50)]
    public string? ContactPhone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }

    // LDAP Configuration
    public string? LdapServer { get; set; }
    public int? LdapPort { get; set; }
    public string? LdapBaseDn { get; set; }
    public string? LdapBindDn { get; set; }
    public string? LdapBindPassword { get; set; }
    public bool LdapEnabled { get; set; } = false;

    // Navigation properties
    public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public virtual ICollection<TenantModule> TenantModules { get; set; } = new List<TenantModule>();
}