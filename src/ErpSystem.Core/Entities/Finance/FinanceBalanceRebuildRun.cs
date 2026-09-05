using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Durable evidence for an explicitly scoped, deterministic balance read-model rebuild.</summary>
public sealed class FinanceBalanceRebuildRun : BusinessEntity
{
    [Required] public Guid AccountingBookId { get; set; }
    [Required, MaxLength(20)] public string AccountingBookCode { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string SourceFingerprint { get; set; } = string.Empty;
    public int BalanceRows { get; set; }
    public int ExposureRows { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal AbsoluteDrift { get; set; }
    public DateTime CompletedAt { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid ApprovedByUserId { get; set; }
    [ForeignKey(nameof(AccountingBookId))] public AccountingBook AccountingBook { get; set; } = null!;
}
