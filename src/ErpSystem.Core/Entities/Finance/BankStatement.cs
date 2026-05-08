using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents an imported bank statement
/// </summary>
public class BankStatement : BaseEntity
{
    [Required]
    public Guid BankAccountId { get; set; }

    public DateTime StatementDate { get; set; }

    [MaxLength(50)]
    public string? StatementNumber { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal ClosingBalance { get; set; }

    public decimal TotalDebits { get; set; }

    public decimal TotalCredits { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public Guid? ImportedBy { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual BankAccount BankAccount { get; set; } = null!;
    public virtual ICollection<BankStatementLine> Lines { get; set; } = new List<BankStatementLine>();
    public virtual ICollection<BankReconciliation> Reconciliations { get; set; } = new List<BankReconciliation>();
}

/// <summary>
/// Represents a line item from a bank statement
/// </summary>
public class BankStatementLine : BaseEntity
{
    [Required]
    public Guid BankStatementId { get; set; }

    public DateTime TransactionDate { get; set; }

    public DateTime? ValueDate { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal Balance { get; set; }

    /// <summary>
    /// Whether this line has been matched to a cash transaction
    /// </summary>
    public bool IsMatched { get; set; }

    public Guid? MatchedTransactionId { get; set; }

    public Guid? ReconciliationMatchId { get; set; }

    // Navigation properties
    public virtual BankStatement BankStatement { get; set; } = null!;
    public virtual CashTransaction? MatchedTransaction { get; set; }
    public virtual ReconciliationMatch? ReconciliationMatch { get; set; }
}
