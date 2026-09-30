using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// One immutable closing decision for one book and fiscal year. Reopening appends reversal
/// evidence; a subsequent close creates another cycle and never overwrites this decision.
/// </summary>
public sealed class YearEndBookCloseCycle : TenantEntity
{
    public Guid FiscalYearId { get; set; }
    public Guid AccountingBookId { get; set; }
    [MaxLength(20)] public string AccountingBookCode { get; set; } = string.Empty;
    [MaxLength(3)] public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public int CycleNumber { get; set; }
    public string PeriodAuthoritySnapshotJson { get; set; } = string.Empty;
    [MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    public Guid RetainedEarningsAccountId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Closing";
    public Guid ClosedByUserId { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    [MaxLength(2000)] public string? ClosingNotes { get; set; }
    public Guid? ClosingJournalEntryId { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetIncomeTransferred { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
    [MaxLength(500)] public string? ReopenReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public FiscalYear FiscalYear { get; set; } = null!;
    public AccountingBook AccountingBook { get; set; } = null!;
    public Account RetainedEarningsAccount { get; set; } = null!;
    public JournalEntry? ClosingJournalEntry { get; set; }
    public JournalEntry? ReversalJournalEntry { get; set; }
}
