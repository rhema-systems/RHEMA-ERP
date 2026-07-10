using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Finance.FixedAssets;

public class FixedAssetDepreciationRun : TenantEntity
{
    [Required]
    public Guid FiscalPeriodId { get; set; }
    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;

    public Guid? FixedAssetId { get; set; }
    public virtual FixedAsset? FixedAsset { get; set; }

    [Required]
    [MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    [Required]
    public DateTime PostingDate { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Created";

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDepreciationAmount { get; set; }

    public Guid? JournalEntryId { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }

    public Guid? PostingEventId { get; set; }
    public virtual FinancePostingEvent? PostingEvent { get; set; }

    [MaxLength(150)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime? CalculatedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? FailedAt { get; set; }

    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    public virtual ICollection<AssetDepreciationSchedule> Lines { get; set; } = new List<AssetDepreciationSchedule>();
}
