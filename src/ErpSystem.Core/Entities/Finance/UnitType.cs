using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a unit of measurement for non-financial quantities.
/// Examples: Employees (headcount), Square Feet (area), Hours (time), Units (production count).
/// Used to categorize Unit Accounts by what they measure.
/// </summary>
public class UnitType : TenantEntity
{
    /// <summary>
    /// Unique code for the unit type (e.g., "EMP", "SQFT", "HRS").
    /// Must be uppercase alphanumeric, unique per tenant.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the unit type (e.g., "Employees", "Square Footage").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description explaining what this unit type measures.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Number of decimal places for quantities of this type.
    /// 0 for whole numbers (e.g., headcount), 2-6 for precision (e.g., hours, weight).
    /// </summary>
    [Required]
    [Range(0, 6)]
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Optional business increment for this UOM (for example 0.125 hours or 0.5 units).
    /// It must not be finer than DecimalPlaces and is independent of currency precision.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? RoundingIncrement { get; set; }

    /// <summary>
    /// Whether this unit type is active and can be used for new accounts.
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    /// <summary>
    /// Unit accounts that use this unit type.
    /// </summary>
    public virtual ICollection<UnitAccount> UnitAccounts { get; set; } = new List<UnitAccount>();
}
