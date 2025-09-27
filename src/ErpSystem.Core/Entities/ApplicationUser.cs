using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Shared;

namespace ErpSystem.Core.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(200)]
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    public AuthenticationProvider AuthenticationProvider { get; set; } = AuthenticationProvider.Local;

    public string? LdapDn { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public DateTime? LastLoginDate { get; set; }
    public string? ProfilePictureUrl { get; set; }

    // Navigation properties
    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();
    public virtual ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    
    // Helper properties for accessing tenant information
    [NotMapped]
    public UserTenant? DefaultTenant => UserTenants.FirstOrDefault(ut => ut.IsDefault && !ut.IsDeleted && ut.Status == UserTenantStatus.Active);
    
    [NotMapped]
    public IEnumerable<Tenant> AccessibleTenants => UserTenants
        .Where(ut => !ut.IsDeleted && 
                    ut.Status == UserTenantStatus.Active && 
                    (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow))
        .Select(ut => ut.Tenant);
    
    // Additional helper properties for different access states
    [NotMapped]
    public IEnumerable<UserTenant> ActiveTenantRelationships => UserTenants
        .Where(ut => !ut.IsDeleted && ut.Status == UserTenantStatus.Active && (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow));
    
    [NotMapped]
    public IEnumerable<UserTenant> SuspendedTenantRelationships => UserTenants
        .Where(ut => !ut.IsDeleted && ut.Status == UserTenantStatus.Suspended);
    
    [NotMapped]
    public IEnumerable<UserTenant> AllTenantRelationships => UserTenants
        .Where(ut => !ut.IsDeleted); // Excludes only soft-deleted relationships
    
    // Primary tenant relationship
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsSystemRole { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class ApplicationUserRole : IdentityUserRole<Guid>
{
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationRole Role { get; set; } = null!;
}

// Junction entity for many-to-many relationship between Users and Tenants
public class UserTenant : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public Guid TenantId { get; set; }
    
    // Access level within this tenant
    public UserTenantAccessLevel AccessLevel { get; set; } = UserTenantAccessLevel.Standard;
    
    // Current status of user access to this tenant
    public UserTenantStatus Status { get; set; } = UserTenantStatus.Active;
    
    // Is this the user's default tenant?
    public bool IsDefault { get; set; } = false;
    
    // When the user was granted access to this tenant
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    
    // When the user's access expires (optional)
    public DateTime? ExpiresAt { get; set; }
    
    // When the user was suspended from this tenant (optional)
    public DateTime? SuspendedAt { get; set; }
    
    // When the user was reactivated in this tenant (optional)
    public DateTime? ReactivatedAt { get; set; }
    
    // Who granted access
    public string? GrantedBy { get; set; }
    
    // Who suspended/reactivated access
    public string? StatusChangedBy { get; set; }
    
    // Additional metadata
    [StringLength(500)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}

// Enum for user access levels within a tenant
public enum UserTenantAccessLevel
{
    // Standard user access
    Standard = 0,
    
    // Elevated access (can manage some tenant settings)
    Elevated = 1,
    
    // Admin access (full tenant management)
    Admin = 2,
    
    // Read-only access (view only)
    ReadOnly = 3
}

// Enum for user status within a tenant
public enum UserTenantStatus
{
    // User has active access to the tenant
    Active = 0,
    
    // User access is temporarily suspended
    Suspended = 1,
    
    // User access is permanently revoked (soft delete alternative)
    Revoked = 2,
    
    // User access is pending approval
    Pending = 3,
    
    // User access has expired
    Expired = 4
}
