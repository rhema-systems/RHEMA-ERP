using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// A specific budget line item amount for an account in a specific period.
/// </summary>
public class BudgetEntry : TenantEntity
{
    [Required]
    public Guid BudgetReturnId { get; set; }

    [ForeignKey(nameof(BudgetReturnId))]
    public virtual BudgetReturn? BudgetReturn { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    [Required]
    public Guid FiscalPeriodId { get; set; }

    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod? FiscalPeriod { get; set; }

    /// <summary>
    /// The currency of the entered amount.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    /// <summary>
    /// Exchange rate used to convert to base currency.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1;

    /// <summary>
    /// The entered budget amount in the source currency.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// The budget amount converted to base currency.
    /// Used for reporting and aggregation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountBase { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
