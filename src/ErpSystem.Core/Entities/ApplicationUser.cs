using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
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
    public string FullName => $"{FirstName} {LastName}";

    [Required]
    public Guid TenantId { get; set; }

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
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<ApplicationUserRole> UserRoles { get; set; } = new List<ApplicationUserRole>();
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
}

public class ApplicationUserRole : IdentityUserRole<Guid>
{
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationRole Role { get; set; } = null!;
}