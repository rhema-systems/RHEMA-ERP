using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Reusable, system-wide reason-code lookup. Scoped by <see cref="Category"/> so the same
/// master list can drive structured reasons across leave adjustments, encashments, and beyond
/// (replacing ad-hoc free-text). Tenant-scoped.
/// </summary>
public class ReasonCode : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public ReasonCodeCategory Category { get; set; } = ReasonCodeCategory.General;

    public bool IsActive { get; set; } = true;
}
