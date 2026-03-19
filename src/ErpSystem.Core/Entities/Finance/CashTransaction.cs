using ErpSystem.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a cash transaction (receipt, payment, or transfer)
/// </summary>
public class CashTransaction : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string TransactionNumber { get; set; } = string.Empty;

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public CashTransactionType TransactionType { get; set; }

    [Required]
    public Guid BankAccountId { get; set; }

    /// <summary>
    /// For transfers, the destination bank account
    /// </summary>
    public Guid? ToBankAccountId { get; set; }

    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>
    /// Exchange rate if different from base currency
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Amount in base currency (GHS)
    /// </summary>
    public decimal BaseAmount { get; set; }

    public Guid? PaymentMethodId { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(200)]
    public string? PayeeOrPayer { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// GL Account to post against (e.g., Sales, Purchases, Expenses)
    /// </summary>
    public Guid? GLAccountId { get; set; }

    /// <summary>
    /// Whether this transaction has been reconciled with bank statement
    /// </summary>
    public bool IsReconciled { get; set; }

    public Guid? ReconciliationId { get; set; }

    /// <summary>
    /// If payment was by cheque
    /// </summary>
    public Guid? ChequeId { get; set; }

    /// <summary>
    /// Whether transaction has been posted to GL
    /// </summary>
    public bool IsPosted { get; set; }

    public DateTime? PostedDate { get; set; }

    public Guid? PostedBy { get; set; }

    // Navigation properties
    public virtual BankAccount BankAccount { get; set; } = null!;
    public virtual BankAccount? ToBankAccount { get; set; }
    public virtual PaymentMethod? PaymentMethod { get; set; }
    public virtual Cheque? Cheque { get; set; }
    public virtual BankReconciliation? Reconciliation { get; set; }
}
