using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Rebuildable foreign-currency exposure for one tenant/account/book/currency. This is posted evidence,
/// not currency-link configuration, and therefore must never be inferred from AccountCurrencyLink.
/// </summary>
public sealed class AccountCurrencyExposure : BusinessEntity
{
    [Required] public Guid AccountId { get; set; }
    [Required] public Guid AccountingBookId { get; set; }
    [Required, MaxLength(20)] public string AccountingBookCode { get; set; } = string.Empty;
    [Required, MaxLength(3)] public string FunctionalCurrencyCode { get; set; } = string.Empty;
    [Required, MaxLength(3)] public string TransactionCurrencyCode { get; set; } = string.Empty;

    /// <summary>Signed debit-minus-credit amount in transaction currency.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SignedForeignBalance { get; set; }

    /// <summary>Signed debit-minus-credit carrying amount in functional currency.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SignedFunctionalBalance { get; set; }

    public int TransactionCount { get; set; }
    public DateTime? FirstTransactionDate { get; set; }
    public DateTime? LastTransactionDate { get; set; }
    public DateTime LastRebuiltAt { get; set; }
    [MaxLength(64)] public string SourceFingerprint { get; set; } = string.Empty;

    [ForeignKey(nameof(AccountId))] public Account Account { get; set; } = null!;
    [ForeignKey(nameof(AccountingBookId))] public AccountingBook AccountingBook { get; set; } = null!;
}
