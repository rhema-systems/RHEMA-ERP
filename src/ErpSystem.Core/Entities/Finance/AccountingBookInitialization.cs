using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Immutable, approved opening authority for one exact accounting book.</summary>
public sealed class AccountingBookInitialization : TenantEntity
{
    public Guid AccountingBookId { get; set; }
    public int Version { get; set; } = 1;
    public Guid? SupersedesInitializationId { get; set; }
    public AccountingBookInitializationMode Mode { get; set; }
    public AccountingBookInitializationStatus InitializationStatus { get; set; } = AccountingBookInitializationStatus.Draft;
    public DateTime CutoffDate { get; set; }
    public Guid CutoffFiscalPeriodId { get; set; }
    public Guid? SourceAccountingBookId { get; set; }
    [MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(500)] public string Reason { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal TotalDebits { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalCredits { get; set; }
    public int RequiredAccountCount { get; set; }
    public int CoveredAccountCount { get; set; }
    [MaxLength(64)] public string EvidenceFingerprint { get; set; } = string.Empty;
    [MaxLength(64)] public string ReconciliationFingerprint { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [MaxLength(500)] public string? DecisionReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public AccountingBook AccountingBook { get; set; } = null!;
    public AccountingBook? SourceAccountingBook { get; set; }
    public FiscalPeriod CutoffFiscalPeriod { get; set; } = null!;
    public AccountingBookInitialization? SupersedesInitialization { get; set; }
    public ICollection<AccountingBookInitializationLine> Lines { get; set; } = new List<AccountingBookInitializationLine>();
}

public sealed class AccountingBookInitializationLine : TenantEntity
{
    public Guid AccountingBookInitializationId { get; set; }
    public Guid AccountId { get; set; }
    [MaxLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal OpeningDebit { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OpeningCredit { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal BaseBookSignedBalance { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OpeningAdjustment { get; set; }

    public AccountingBookInitialization Initialization { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
