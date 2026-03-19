using ErpSystem.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a cheque issued or received
/// </summary>
public class Cheque : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string ChequeNumber { get; set; } = string.Empty;

    [Required]
    public Guid BankAccountId { get; set; }

    public DateTime IssueDate { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? PayeeName { get; set; }

    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public ChequeStatus Status { get; set; } = ChequeStatus.Issued;

    public DateTime? PresentedDate { get; set; }

    public DateTime? ClearedDate { get; set; }

    public DateTime? CancelledDate { get; set; }

    [MaxLength(500)]
    public string? Memo { get; set; }

    /// <summary>
    /// Link to the cash transaction this cheque is for
    /// </summary>
    public Guid? CashTransactionId { get; set; }

    /// <summary>
    /// Reason for cancellation or bouncing
    /// </summary>
    [MaxLength(500)]
    public string? StatusReason { get; set; }

    // Navigation properties
    public virtual BankAccount BankAccount { get; set; } = null!;
    public virtual CashTransaction? CashTransaction { get; set; }
}

/// <summary>
/// Represents a payment method (cash, cheque, EFT, mobile money, etc.)
/// </summary>
public class PaymentMethod : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Code { get; set; }

    public PaymentMethodType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this payment method requires a bank account
    /// </summary>
    public bool RequiresBankAccount { get; set; } = true;

    /// <summary>
    /// Whether this payment method requires a reference number
    /// </summary>
    public bool RequiresReference { get; set; }

    /// <summary>
    /// Default GL account for this payment method
    /// </summary>
    public Guid? DefaultGLAccountId { get; set; }

    // Navigation properties
    public virtual ICollection<CashTransaction> Transactions { get; set; } = new List<CashTransaction>();
}
