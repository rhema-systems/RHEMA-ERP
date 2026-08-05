using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// Read/display DTO for a staff level.
/// A staff level represents a standardized organizational level used for ranking or grouping roles.
/// </summary>
public class StaffLevelDto
{
    /// <summary>
    /// Unique identifier of the staff level.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this staff level.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Display name of the staff level.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional business code for the staff level.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Ordering/ranking value (lower typically indicates more junior, depending on business rules).
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Optional description to explain usage or scope.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the staff level is active and available for assignment.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO used to create a staff level.
/// This model intentionally excludes identifiers, tenant context, and audit fields.
/// </summary>
public class CreateStaffLevelDto
{
    /// <summary>
    /// Display name of the staff level.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional business code for the staff level.
    /// </summary>
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Ordering/ranking value.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Rank { get; set; } = 1;

    /// <summary>
    /// Optional description to explain usage or scope.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// DTO used to update an existing staff level.
/// </summary>
public class UpdateStaffLevelDto
{
    /// <summary>
    /// Unique identifier of the staff level.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the staff level.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional business code for the staff level.
    /// </summary>
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Ordering/ranking value.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Rank { get; set; } = 1;

    /// <summary>
    /// Optional description to explain usage or scope.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether the staff level is active and available for assignment.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Optimized DTO for staff-level table/list views.
/// </summary>
public class StaffLevelListDto
{
    /// <summary>
    /// Unique identifier of the staff level.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the staff level.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional business code for the staff level.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Ordering/ranking value.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Whether the staff level is active.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Detailed view DTO for a staff level.
/// Includes computed fields suitable for UI detail pages.
/// </summary>
public class StaffLevelDetailDto
{
    /// <summary>
    /// Unique identifier of the staff level.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant that owns this staff level.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Display name of the staff level.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional business code for the staff level.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Ordering/ranking value.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Optional description to explain usage or scope.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the staff level is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Audit timestamp: when this record was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Computed count of employee positions assigned to this staff level.
    /// </summary>
    public int EmployeePositionCount { get; set; }
}
