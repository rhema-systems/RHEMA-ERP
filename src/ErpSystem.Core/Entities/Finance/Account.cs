using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    Expense = 5
}

public class Account : BusinessEntity
{
    [Required]
    [MaxLength(20)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;

    [Required]
    public AccountType AccountType { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? ParentAccountId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DebitBalance { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditBalance { get; set; } = 0;

    public new bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Account? ParentAccount { get; set; }
    public virtual ICollection<Account> ChildAccounts { get; set; } = new List<Account>();
    public virtual ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();
}
