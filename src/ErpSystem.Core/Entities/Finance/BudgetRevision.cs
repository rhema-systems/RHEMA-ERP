using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// A governed request to change the currently adopted budget without editing that
/// approved baseline in place.  Once Board approval is complete, Finance applies
/// the request by creating and adopting an immutable successor scenario.
/// </summary>
public class BudgetRevision : TenantEntity
{
    [Required]
    [MaxLength(40)]
    public string RevisionNumber { get; set; } = string.Empty;

    /// <summary>Supported values are Virement and Supplementary.</summary>
    [Required]
    [MaxLength(20)]
    public string RevisionType { get; set; } = "Virement";

    [Required]
    public Guid SourceScenarioId { get; set; }

    [ForeignKey(nameof(SourceScenarioId))]
    public virtual BudgetScenario? SourceScenario { get; set; }

    /// <summary>
    /// Set only after application.  Keeping this link on the request makes the
    /// Board authority, resulting approved figures, and prior baseline directly traceable.
    /// </summary>
    public Guid? ResultScenarioId { get; set; }

    [ForeignKey(nameof(ResultScenarioId))]
    public virtual BudgetScenario? ResultScenario { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string BoardResolutionReference { get; set; } = string.Empty;

    [Required]
    public DateTime BoardResolutionDate { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Justification { get; set; } = string.Empty;

    /// <summary>Draft, Submitted, Approved, Rejected, or Applied.</summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? AppliedAt { get; set; }
    public Guid? AppliedByUserId { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<BudgetRevisionLine> Lines { get; set; } = new List<BudgetRevisionLine>();
}

/// <summary>
/// A signed base-currency change to one cost-centre/account/period budget cell.
/// Negative values release budget; positive values increase budget.
/// </summary>
public class BudgetRevisionLine : TenantEntity
{
    [Required]
    public Guid BudgetRevisionId { get; set; }

    [ForeignKey(nameof(BudgetRevisionId))]
    public virtual BudgetRevision? BudgetRevision { get; set; }

    public Guid? SegmentValueId { get; set; }

    [ForeignKey(nameof(SegmentValueId))]
    public virtual SegmentLookupValue? SegmentValue { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [ForeignKey(nameof(AccountId))]
    public virtual Account? Account { get; set; }

    [Required]
    public Guid FiscalPeriodId { get; set; }

    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod? FiscalPeriod { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AdjustmentAmountBase { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
