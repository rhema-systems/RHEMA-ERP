using ErpSystem.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a bank reconciliation process
/// </summary>
public class BankReconciliation : BaseEntity
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid BankAccountId { get; set; }

    public DateTime ReconciliationDate { get; set; } = DateTime.UtcNow;

    public Guid? StatementId { get; set; }

    /// <summary>
    /// Balance per bank statement
    /// </summary>
    public decimal StatementBalance { get; set; }

    /// <summary>
    /// Balance per our books
    /// </summary>
    public decimal BookBalance { get; set; }

    /// <summary>
    /// Difference between statement and book balance
    /// </summary>
    public decimal Difference { get; set; }

    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Pending;

    public int MatchedCount { get; set; }

    public int UnmatchedBookCount { get; set; }

    public int UnmatchedStatementCount { get; set; }

    public Guid? ReconciledBy { get; set; }

    public DateTime? ReconciledAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual BankAccount BankAccount { get; set; } = null!;
    public virtual BankStatement? Statement { get; set; }
    public virtual ICollection<ReconciliationMatch> Matches { get; set; } = new List<ReconciliationMatch>();
    public virtual ICollection<CashTransaction> ReconciledTransactions { get; set; } = new List<CashTransaction>();
}

/// <summary>
/// Represents a match between a cash transaction and a bank statement line
/// </summary>
public class ReconciliationMatch : BaseEntity
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid ReconciliationId { get; set; }

    [Required]
    public Guid CashTransactionId { get; set; }

    [Required]
    public Guid BankStatementLineId { get; set; }

    /// <summary>
    /// Whether this was an automatic match or manual
    /// </summary>
    public bool IsAutoMatched { get; set; }

    /// <summary>
    /// Confidence score for auto-matches (0-100)
    /// </summary>
    public int? MatchConfidence { get; set; }

    public Guid? MatchedBy { get; set; }

    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual BankReconciliation Reconciliation { get; set; } = null!;
    public virtual CashTransaction CashTransaction { get; set; } = null!;
    public virtual BankStatementLine BankStatementLine { get; set; } = null!;
}
