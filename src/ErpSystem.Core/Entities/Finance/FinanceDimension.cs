using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Finance-owned transaction dimension. Structural chart-of-account segments remain in
/// <see cref="AccountSegmentStructure"/>; this entity governs classifications carried by an
/// individual journal or source-document line independently of the natural GL account.
/// </summary>
public sealed class FinanceDimensionDefinition : TenantEntity
{
    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Analytical, Balancing, or Derived.</summary>
    [Required, MaxLength(20)]
    public string Classification { get; set; } = "Analytical";

    /// <summary>Lookup or EntityBacked.</summary>
    [Required, MaxLength(20)]
    public string ValueSourceType { get; set; } = "Lookup";

    /// <summary>Canonical operational entity type when values are backed by another module.</summary>
    [MaxLength(100)]
    public string? SourceEntityType { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<FinanceDimensionValue> Values { get; set; } = new List<FinanceDimensionValue>();
    public ICollection<FinanceDimensionAccountRule> AccountRules { get; set; } = new List<FinanceDimensionAccountRule>();
}

/// <summary>Effective-dated, tenant-owned value for one transaction dimension.</summary>
public sealed class FinanceDimensionValue : TenantEntity
{
    [Required]
    public Guid FinanceDimensionDefinitionId { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentValueId { get; set; }

    [MaxLength(100)]
    public string? SourceEntityType { get; set; }

    public Guid? SourceEntityId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(FinanceDimensionDefinitionId))]
    public FinanceDimensionDefinition FinanceDimensionDefinition { get; set; } = null!;

    [ForeignKey(nameof(ParentValueId))]
    public FinanceDimensionValue? ParentValue { get; set; }

    public ICollection<FinanceDimensionValue> ChildValues { get; set; } = new List<FinanceDimensionValue>();
}

/// <summary>
/// Deduplicated immutable combination of dimension values. Posted ledger lines reference the set;
/// they never recalculate their historical classifications from current defaults or master data.
/// </summary>
public sealed class FinanceDimensionSet : TenantEntity
{
    [Required, MaxLength(64)]
    public string CombinationHash { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string DisplayValue { get; set; } = string.Empty;

    public ICollection<FinanceDimensionSetItem> Items { get; set; } = new List<FinanceDimensionSetItem>();
    public ICollection<AccountTransaction> AccountTransactions { get; set; } = new List<AccountTransaction>();
}

/// <summary>Frozen value and readable snapshots belonging to one immutable dimension set.</summary>
public sealed class FinanceDimensionSetItem : TenantEntity
{
    [Required]
    public Guid FinanceDimensionSetId { get; set; }

    [Required]
    public Guid FinanceDimensionDefinitionId { get; set; }

    [Required]
    public Guid FinanceDimensionValueId { get; set; }

    [Required, MaxLength(30)]
    public string DimensionCodeSnapshot { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string DimensionValueCodeSnapshot { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DimensionValueNameSnapshot { get; set; } = string.Empty;

    [ForeignKey(nameof(FinanceDimensionSetId))]
    public FinanceDimensionSet FinanceDimensionSet { get; set; } = null!;

    [ForeignKey(nameof(FinanceDimensionDefinitionId))]
    public FinanceDimensionDefinition FinanceDimensionDefinition { get; set; } = null!;

    [ForeignKey(nameof(FinanceDimensionValueId))]
    public FinanceDimensionValue FinanceDimensionValue { get; set; } = null!;
}

/// <summary>
/// Account-specific applicability rule. Phase 1 persists the policy without enforcing it against
/// legacy producers; Required enforcement is activated only after each posting adapter is certified.
/// </summary>
public sealed class FinanceDimensionAccountRule : TenantEntity
{
    [Required]
    public Guid AccountId { get; set; }

    [Required]
    public Guid FinanceDimensionDefinitionId { get; set; }

    /// <summary>Required, Optional, Prohibited, or Fixed.</summary>
    [Required, MaxLength(20)]
    public string RuleType { get; set; } = "Optional";

    /// <summary>
    /// Optional posting-source scope. Non-optional controls must identify the producer they have
    /// been certified for; null scope remains a harmless, descriptive Optional rule.
    /// </summary>
    [MaxLength(50)]
    public string? SourceModule { get; set; }

    [MaxLength(100)]
    public string? SourceDocumentType { get; set; }

    [MaxLength(50)]
    public string? PostingAction { get; set; }

    public Guid? DefaultDimensionValueId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AccountId))]
    public Account Account { get; set; } = null!;

    [ForeignKey(nameof(FinanceDimensionDefinitionId))]
    public FinanceDimensionDefinition FinanceDimensionDefinition { get; set; } = null!;

    [ForeignKey(nameof(DefaultDimensionValueId))]
    public FinanceDimensionValue? DefaultDimensionValue { get; set; }
}
