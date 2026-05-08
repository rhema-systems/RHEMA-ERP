using ErpSystem.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a bank account for cash management
/// </summary>
public class BankAccount : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BankBranch { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS"; // ISO 4217 currency code

    public BankAccountType AccountType { get; set; } = BankAccountType.Checking;

    /// <summary>
    /// Link to GL Account for posting transactions
    /// </summary>
    public Guid? GLAccountId { get; set; }

    /// <summary>
    /// Current book balance (from our records)
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// Available balance (current - holds/pending)
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// Opening balance when account was created
    /// </summary>
    public decimal OpeningBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime OpeningDate { get; set; } = DateTime.UtcNow;

    public DateTime? ClosingDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual ICollection<CashTransaction> Transactions { get; set; } = new List<CashTransaction>();
    public virtual ICollection<BankStatement> Statements { get; set; } = new List<BankStatement>();
    public virtual ICollection<BankReconciliation> Reconciliations { get; set; } = new List<BankReconciliation>();
}
