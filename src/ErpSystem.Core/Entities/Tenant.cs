using System.ComponentModel.DataAnnotations;
using ErpSystem.Shared;
using ErpSystem.Shared.Interfaces;

namespace ErpSystem.Core.Entities;

public class Tenant : BaseEntity, IAuditable
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

    // Branding and theme properties
    [StringLength(7)] // Hex color code
    public string? PrimaryColor { get; set; }

    [StringLength(7)] // Hex color code
    public string? SecondaryColor { get; set; }

    [StringLength(500)]
    public string? FaviconUrl { get; set; }

    [StringLength(500)]
    public string? CoverImageUrl { get; set; }

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

    // Default tenant settings
    public bool IsDefaultForPublicUsers { get; set; } = false;
    public bool IsDefaultForInternalUsers { get; set; } = false;

    // Feature flags
    public bool AllowSelfRegistration { get; set; } = false;
    public string? PublicRegistrationDomains { get; set; }
    public bool RequireEmailVerification { get; set; } = false;
    public int UserAudience { get; set; } = 2; // Internal (1), External (2), Both (3)
    public string? WelcomeMessage { get; set; }
    public int DefaultPriority { get; set; } = 10;
    public bool EnableAutoSelection { get; set; } = false;

    // Finance Configuration
    /// <summary>
    /// Base currency for this tenant (functional currency per IAS 21).
    /// All financial transactions are recorded and reported in this currency.
    /// ISO 4217 three-letter currency code (e.g., "GHS", "USD", "EUR", "NGN").
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string BaseCurrency { get; set; } = "GHS"; // Default to Ghana Cedis

    /// <summary>
    /// Display name for base currency (e.g., "Ghana Cedis", "US Dollar").
    /// </summary>
    [MaxLength(50)]
    public string? BaseCurrencyName { get; set; }

    /// <summary>
    /// Currency symbol for display (e.g., "₵", "$", "€", "₦").
    /// </summary>
    [MaxLength(5)]
    public string? CurrencySymbol { get; set; }

    /// <summary>
    /// Number of decimal places for currency amounts (typically 2).
    /// </summary>
    public int CurrencyDecimalPlaces { get; set; } = 2;

    // Navigation properties
    public virtual ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    public virtual ICollection<TenantModule> TenantModules { get; set; } = new List<TenantModule>();

    // Helper properties for accessing user information
    public IEnumerable<ApplicationUser> Users => UserTenants.Where(ut => !ut.IsDeleted && (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow)).Select(ut => ut.User);
    public int ActiveUserCount => UserTenants.Count(ut => !ut.IsDeleted && (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow));
}
