using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Tracks period-by-period balances for unit accounts.
/// Similar to AccountBalance but for unit quantities.
/// Enables period-over-period analysis and historical reporting.
/// </summary>
public class UnitAccountBalance : TenantEntity
{
    // ========================================================================
    // ACCOUNT RELATIONSHIP
    // ========================================================================

    /// <summary>
    /// Foreign key to the unit account.
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
    // BALANCE VALUES
    // ========================================================================

    /// <summary>
    /// Balance at the start of the period.
    /// Should equal prior period's closing balance.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal OpeningBalance { get; set; } = 0;

    /// <summary>
    /// Net quantity change during the period.
    /// Sum of all posted journal entry lines.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal PeriodActivity { get; set; } = 0;

    /// <summary>
    /// Balance at the end of the period.
    /// OpeningBalance + PeriodActivity = ClosingBalance
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal ClosingBalance { get; set; } = 0;
}
