using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// A tenant-controlled store or settlement channel for monetary value. Bank accounts remain
/// specialised bank masters; this record supplies the common liquidity/subledger abstraction.
/// </summary>
public class LiquidityAccount : TenantEntity
{
    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public LiquidityAccountType AccountType { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Required]
    public Guid GLAccountId { get; set; }
    public virtual Account GLAccount { get; set; } = null!;

    /// <summary>
    /// Present only for the Bank subtype. It keeps existing bank-specific configuration and
    /// statement/reconciliation behaviour separate from non-bank holding accounts.
    /// </summary>
    public Guid? BankAccountId { get; set; }
    public virtual BankAccount? BankAccount { get; set; }

    [MaxLength(100)]
    public string? ProviderName { get; set; }

    [MaxLength(100)]
    public string? ProviderAccountReference { get; set; }

    public bool AllowsNegativeBalance { get; set; }
    public bool AllowsManualAllocations { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsSystemAccount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<LiquidityAccountEntry> Entries { get; set; } = new List<LiquidityAccountEntry>();
}

/// <summary>
/// Immutable operational subledger entry. Allocation totals are updated under serializable
/// transactions so partial banking cannot over-consume a source document.
/// </summary>
public class LiquidityAccountEntry : TenantEntity
{
    [Required]
    public Guid LiquidityAccountId { get; set; }
    public virtual LiquidityAccount LiquidityAccount { get; set; } = null!;

    [Required, MaxLength(50)]
    public string EntryNumber { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; }
    public LiquidityEntryType EntryType { get; set; }
    public LiquidityEntryDirection Direction { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [NotMapped]
    public decimal RemainingAmount => Math.Max(Amount - AllocatedAmount, 0m);

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Required, MaxLength(80)]
    public string SourceDocumentType { get; set; } = string.Empty;

    [Required]
    public Guid SourceDocumentId { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(200)]
    public string? CounterpartyName { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsReversed { get; set; }
    public Guid? ReversalOfEntryId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<BankDepositAllocation> DepositAllocations { get; set; } = new List<BankDepositAllocation>();
}

/// <summary>
/// One bank-facing settlement footprint. A posted batch creates exactly one bank transaction
/// for NetAmount, which remains compatible with one-to-one bank reconciliation matching.
/// </summary>
public class BankDepositBatch : TenantEntity
{
    [Required, MaxLength(50)]
    public string DepositNumber { get; set; } = string.Empty;

    [Required]
    public Guid BankAccountId { get; set; }
    public virtual BankAccount BankAccount { get; set; } = null!;

    public DateTime DepositDate { get; set; }

    [Required, MaxLength(100)]
    public string DepositReference { get; set; } = string.Empty;

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public BankDepositStatus Status { get; set; } = BankDepositStatus.Draft;
    public DepositPolicy PolicySnapshot { get; set; } = DepositPolicy.DepositIntact;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalReceipts { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDeductions { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAmount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? PostedById { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    public Guid? JournalEntryId { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }

    public Guid? CashTransactionId { get; set; }
    public virtual CashTransaction? CashTransaction { get; set; }

    public Guid? ReversalJournalEntryId { get; set; }
    public virtual JournalEntry? ReversalJournalEntry { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<BankDepositAllocation> Allocations { get; set; } = new List<BankDepositAllocation>();
    public virtual ICollection<BankDepositAttachment> Attachments { get; set; } = new List<BankDepositAttachment>();
}

public class BankDepositAllocation : TenantEntity
{
    [Required]
    public Guid BankDepositBatchId { get; set; }
    public virtual BankDepositBatch BankDepositBatch { get; set; } = null!;

    [Required]
    public Guid LiquidityAccountEntryId { get; set; }
    public virtual LiquidityAccountEntry LiquidityAccountEntry { get; set; } = null!;

    public BankDepositAllocationType AllocationType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class BankDepositAttachment : TenantEntity
{
    public Guid BankDepositBatchId { get; set; }
    public virtual BankDepositBatch BankDepositBatch { get; set; } = null!;

    public Guid FileUploadRecordId { get; set; }
    public virtual FileUploadRecord FileUploadRecord { get; set; } = null!;

    [Required, MaxLength(50)]
    public string DocumentType { get; set; } = "Other";

    public bool IsPrimaryEvidence { get; set; }
}

/// <summary>
/// Controlled handling of a deposited customer cheque subsequently returned by the bank.
/// The original deposit is retained; posting creates the bank debit and reopens AR allocations.
/// </summary>
public class ReturnedChequeCase : TenantEntity
{
    [Required, MaxLength(50)]
    public string CaseNumber { get; set; } = string.Empty;

    [Required]
    public Guid CustomerPaymentId { get; set; }
    public virtual CustomerPayment CustomerPayment { get; set; } = null!;

    public Guid? BankDepositBatchId { get; set; }
    public virtual BankDepositBatch? BankDepositBatch { get; set; }

    [Required]
    public Guid BankAccountId { get; set; }
    public virtual BankAccount BankAccount { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ChequeNumber { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? DrawerBank { get; set; }

    public DateTime ReturnDate { get; set; }

    [Required, MaxLength(100)]
    public string BankReference { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string ReturnReason { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReturnedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BankChargeAmount { get; set; }

    public ReturnedChequeChargeTreatment ChargeTreatment { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CustomerRecoverableChargeAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpenseChargeAmount { get; set; }

    public ReturnedChequeCaseStatus Status { get; set; } = ReturnedChequeCaseStatus.Draft;
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? PostedAt { get; set; }

    public Guid? JournalEntryId { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }

    public Guid? ReturnCashTransactionId { get; set; }
    public virtual CashTransaction? ReturnCashTransaction { get; set; }

    public Guid? ChargeCashTransactionId { get; set; }
    public virtual CashTransaction? ChargeCashTransaction { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<ReturnedChequeAttachment> Attachments { get; set; } = new List<ReturnedChequeAttachment>();
}

public class ReturnedChequeAttachment : TenantEntity
{
    public Guid ReturnedChequeCaseId { get; set; }
    public virtual ReturnedChequeCase ReturnedChequeCase { get; set; } = null!;

    public Guid FileUploadRecordId { get; set; }
    public virtual FileUploadRecord FileUploadRecord { get; set; } = null!;

    [Required, MaxLength(50)]
    public string DocumentType { get; set; } = "Other";

    public bool IsPrimaryEvidence { get; set; }
}
