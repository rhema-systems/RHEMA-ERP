using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Exact-book inner posting authority; tenant fiscal locks remain the outer authority.</summary>
public sealed class AccountingBookPeriod : TenantEntity
{
    public Guid AccountingBookId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public AccountingBookPeriodStatus PeriodStatus { get; set; } = AccountingBookPeriodStatus.Future;
    public AccountingBookPeriodStatus? PendingStatus { get; set; }
    [MaxLength(500)] public string? PendingReason { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? RequestedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [MaxLength(500)] public string? DecisionReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public AccountingBook AccountingBook { get; set; } = null!;
    public FiscalPeriod FiscalPeriod { get; set; } = null!;
}
