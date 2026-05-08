using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Budget entry for a unit account in a specific fiscal period.
/// Enables budget vs actual variance analysis for statistical quantities.
/// </summary>
public class UnitAccountBudget : TenantEntity
{
    // ========================================================================
    // ACCOUNT RELATIONSHIP
    // ========================================================================

    /// <summary>
    /// Foreign key to the unit account being budgeted.
    /// </summary>
    [Required]
    public Guid UnitAccountId { get; set; }

    /// <summary>
    /// Navigation property to the unit account.
    /// </summary>
    [ForeignKey(nameof(UnitAccountId))]
    public virtual UnitAccount? UnitAccount { get; set; }

    // ========================================================================
    // FISCAL PERIOD
    // ========================================================================

    /// <summary>
    /// Foreign key to the fiscal year.
    /// </summary>
    [Required]
    public Guid FiscalYearId { get; set; }

    /// <summary>
    /// Navigation property to fiscal year.
    /// </summary>
    [ForeignKey(nameof(FiscalYearId))]
    public virtual FiscalYear? FiscalYear { get; set; }

    /// <summary>
    /// Foreign key to the fiscal period.
    /// </summary>
    [Required]
    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// Navigation property to fiscal period.
    /// </summary>
    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod? FiscalPeriod { get; set; }

    // ========================================================================
    // BUDGET VALUES
    // ========================================================================

    /// <summary>
    /// Budgeted quantity for the period.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal BudgetQuantity { get; set; } = 0;

    /// <summary>
    /// Optional notes about the budget entry.
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Version/scenario name for the budget (e.g., "Original", "Revised Q3").
    /// Allows multiple budget scenarios.
    /// </summary>
    [MaxLength(50)]
    public string BudgetVersion { get; set; } = "Original";

    /// <summary>
    /// Whether this is the active/current budget version.
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;
}
