using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a high-level budget version or scenario for a fiscal year.
/// Examples: "FY2026 Original", "FY2026 Q1 Forecast".
/// </summary>
public class BudgetScenario : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid FiscalYearId { get; set; }

    [ForeignKey(nameof(FiscalYearId))]
    public virtual FiscalYear? FiscalYear { get; set; }

    /// <summary>
    /// The base currency for this budget scenario.
    /// Typically matches the Fiscal Year base currency.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string BaseCurrencyCode { get; set; } = "GHS";

    /// <summary>
    /// Indicates if this is the primary approved budget used for standard variance reporting.
    /// Only one scenario per Fiscal Year should be active at a time.
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Status of the overall budget scenario.
    /// Values: Draft, Open, Locked, Archived.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Date when this scenario was locked/finalized.
    /// </summary>
    public DateTime? LockedDate { get; set; }

    public Guid? LockedByUserId { get; set; }

    public virtual ICollection<BudgetReturn> BudgetReturns { get; set; } = new List<BudgetReturn>();
}
