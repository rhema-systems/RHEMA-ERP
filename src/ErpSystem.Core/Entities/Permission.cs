using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Represents a permission that can be assigned to roles
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>
    /// Unique identifier for the permission (e.g., "users.read", "roles.create")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the permission
    /// </summary>
    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this permission allows
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Category or module this permission belongs to (e.g., "User Management", "Finance")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a system permission that cannot be deleted
    /// </summary>
    public bool IsSystemPermission { get; set; } = false;

    /// <summary>
    /// Navigation property for role-permission relationships
    /// </summary>
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Junction entity for many-to-many relationship between Roles and Permissions
/// </summary>
public class RolePermission
{
    /// <summary>
    /// Foreign key to the Role
    /// </summary>
    [Required]
    public Guid RoleId { get; set; }

    /// <summary>
    /// Foreign key to the Permission
    /// </summary>
    [Required]
    public Guid PermissionId { get; set; }

    /// <summary>
    /// When this permission was granted to the role
    /// </summary>
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who granted this permission to the role
    /// </summary>
    [StringLength(100)]
    public string? GrantedBy { get; set; }

    /// <summary>
    /// Navigation property to the Role
    /// </summary>
    public virtual ApplicationRole Role { get; set; } = null!;

    /// <summary>
    /// Navigation property to the Permission
    /// </summary>
    public virtual Permission Permission { get; set; } = null!;
}
