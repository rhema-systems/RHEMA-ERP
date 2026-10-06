using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a high-level budget version or scenario for a fiscal year.
/// Examples: "FY2026 Original", "FY2026 Q1 Forecast".
/// </summary>
public class BudgetScenario : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Human-readable version classification used in official-budget history.
    /// Original scenarios are created through normal planning; applied requests
    /// create Virement or Supplementary successors.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string VersionType { get; set; } = "Original";

    /// <summary>Monotonically increasing official version within a fiscal year.</summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>The official scenario copied to produce this immutable version.</summary>
    public Guid? ParentScenarioId { get; set; }

    [ForeignKey(nameof(ParentScenarioId))]
    public virtual BudgetScenario? ParentScenario { get; set; }

    [Required]
    public Guid FiscalYearId { get; set; }

    [ForeignKey(nameof(FiscalYearId))]
    public virtual FiscalYear? FiscalYear { get; set; }

    /// <summary>
    /// The base currency for this budget scenario.
    /// Typically matches the Fiscal Year base currency.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string BaseCurrencyCode { get; set; } = "GHS";

    /// <summary>
    /// Indicates if this is the primary approved budget used for standard variance reporting.
    /// Only one scenario per Fiscal Year should be active at a time.
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Status of the overall budget scenario.
    /// Values: Draft, Collecting, InReview, Approved, Superseded, Archived.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Date when this scenario was locked/finalized.
    /// </summary>
    public DateTime? LockedDate { get; set; }

    public Guid? LockedByUserId { get; set; }

    [ForeignKey(nameof(LockedByUserId))]
    public virtual ApplicationUser? LockedByUser { get; set; }

    /// <summary>
    /// Date and time when this approved scenario was explicitly adopted as the
    /// official reporting baseline.
    /// </summary>
    public DateTime? AdoptedAt { get; set; }

    /// <summary>
    /// Business-effective date of the official budget adoption.
    /// </summary>
    public DateTime? AdoptionEffectiveDate { get; set; }

    public Guid? AdoptedByUserId { get; set; }

    [ForeignKey(nameof(AdoptedByUserId))]
    public virtual ApplicationUser? AdoptedByUser { get; set; }

    [MaxLength(1000)]
    public string? AdoptionReason { get; set; }

    /// <summary>
    /// Populated when another approved scenario replaces this one as the official baseline.
    /// </summary>
    public DateTime? SupersededAt { get; set; }

    public Guid? SupersededByUserId { get; set; }

    [ForeignKey(nameof(SupersededByUserId))]
    public virtual ApplicationUser? SupersededByUser { get; set; }

    [MaxLength(1000)]
    public string? SupersessionReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<BudgetReturn> BudgetReturns { get; set; } = new List<BudgetReturn>();
    public virtual ICollection<BudgetScenarioControlDimension> ControlDimensions { get; set; } = new List<BudgetScenarioControlDimension>();
    public virtual ICollection<BudgetScenarioControlSegment> ControlSegments { get; set; } = new List<BudgetScenarioControlSegment>();
}

/// <summary>
/// Declares one transaction dimension that forms part of every budget cell in a scenario.
/// The declaration is scenario-owned so later changes to account defaults cannot silently
/// change the grain of an adopted budget.
/// </summary>
public sealed class BudgetScenarioControlDimension : TenantEntity
{
    [Required]
    public Guid BudgetScenarioId { get; set; }

    [Required]
    public Guid FinanceDimensionDefinitionId { get; set; }

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(BudgetScenarioId))]
    public BudgetScenario BudgetScenario { get; set; } = null!;

    [ForeignKey(nameof(FinanceDimensionDefinitionId))]
    public FinanceDimensionDefinition FinanceDimensionDefinition { get; set; } = null!;
}

/// <summary>
/// Declares an account-number segment structure that can scope distributed returns
/// for this scenario. Each return currently selects at most one lookup value from one
/// of the declared structures, matching the existing SegmentValueId budget-control grain.
/// </summary>
public sealed class BudgetScenarioControlSegment : TenantEntity
{
    [Required]
    public Guid BudgetScenarioId { get; set; }

    [Required]
    public Guid AccountSegmentStructureId { get; set; }

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(BudgetScenarioId))]
    public BudgetScenario BudgetScenario { get; set; } = null!;

    [ForeignKey(nameof(AccountSegmentStructureId))]
    public AccountSegmentStructure AccountSegmentStructure { get; set; } = null!;
}
