using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Type of allocation method.
/// </summary>
public enum AllocationType
{
    /// <summary>Allocate based on unit account balance ratios.</summary>
    UnitAccountBased = 0,
    
    /// <summary>Allocate based on fixed percentages.</summary>
    FixedPercentage = 1,
    
    /// <summary>Equal allocation across all targets.</summary>
    EqualDistribution = 2
}

/// <summary>
/// Defines an allocation rule for distributing financial amounts.
/// Uses Unit Account balances as allocation drivers (similar to Dynamics GP Allocation Accounts).
/// Example: Allocate overhead costs based on employee headcount per department.
/// </summary>
public class AllocationRule : TenantEntity
{
    // ========================================================================
    // RULE IDENTIFICATION
    // ========================================================================

    /// <summary>
    /// Unique code for the allocation rule.
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the allocation rule.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this allocation achieves.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    // ========================================================================
    // SOURCE CONFIGURATION
    // ========================================================================

    /// <summary>
    /// Source GL account from which amounts are allocated.
    /// </summary>
    [Required]
    public Guid SourceAccountId { get; set; }

    /// <summary>
    /// Navigation property to source GL account.
    /// </summary>
    [ForeignKey(nameof(SourceAccountId))]
    public virtual Account? SourceAccount { get; set; }

    // ========================================================================
    // ALLOCATION METHOD
    // ========================================================================

    /// <summary>
    /// How the allocation is calculated.
    /// </summary>
    [Required]
    public AllocationType AllocationType { get; set; } = AllocationType.UnitAccountBased;

    /// <summary>
    /// Unit account used as the allocation driver/basis.
    /// Required when AllocationType is UnitAccountBased.
    /// Example: Employee headcount account for headcount-based allocation.
    /// </summary>
    public Guid? DriverUnitAccountId { get; set; }

    /// <summary>
    /// Navigation property to driver unit account.
    /// </summary>
    [ForeignKey(nameof(DriverUnitAccountId))]
    public virtual UnitAccount? DriverUnitAccount { get; set; }

    // ========================================================================
    // STATUS AND SCHEDULING
    // ========================================================================

    /// <summary>
    /// Whether this rule is active.
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(30)]
    public string ApprovalStatus { get; set; } = "Draft";

    /// <summary>
    /// Whether to auto-reverse allocations at period end.
    /// </summary>
    public bool AutoReverse { get; set; } = false;

    /// <summary>
    /// Last date this allocation was run.
    /// </summary>
    public DateTime? LastRunDate { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    /// <summary>
    /// Target accounts for this allocation.
    /// </summary>
    public virtual ICollection<AllocationTarget> Targets { get; set; } = new List<AllocationTarget>();
}

/// <summary>
/// Defines a target account for an allocation rule.
/// Each target receives a portion of the allocated amount.
/// </summary>
public class AllocationTarget : TenantEntity
{
    // ========================================================================
    // PARENT RULE RELATIONSHIP
    // ========================================================================

    /// <summary>
    /// Foreign key to the parent allocation rule.
    /// </summary>
    [Required]
    public Guid AllocationRuleId { get; set; }

    /// <summary>
    /// Navigation property to the parent rule.
    /// </summary>
    [ForeignKey(nameof(AllocationRuleId))]
    public virtual AllocationRule? AllocationRule { get; set; }

    // ========================================================================
    // TARGET ACCOUNT
    // ========================================================================

    /// <summary>
    /// Target GL account to receive allocated amount.
    /// </summary>
    [Required]
    public Guid TargetAccountId { get; set; }

    /// <summary>
    /// Navigation property to target GL account.
    /// </summary>
    [ForeignKey(nameof(TargetAccountId))]
    public virtual Account? TargetAccount { get; set; }

    // ========================================================================
    // ALLOCATION BASIS
    // ========================================================================

    /// <summary>
    /// Fixed percentage for this target (when using FixedPercentage allocation).
    /// Value between 0 and 100.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? FixedPercentage { get; set; }

    /// <summary>
    /// Child unit account under the driver account for this target.
    /// Used when allocating based on child account ratios.
    /// Example: Department-specific employee count under Total Employees.
    /// </summary>
    public Guid? TargetDriverUnitAccountId { get; set; }

    /// <summary>
    /// Navigation property to target driver unit account.
    /// </summary>
    [ForeignKey(nameof(TargetDriverUnitAccountId))]
    public virtual UnitAccount? TargetDriverUnitAccount { get; set; }

    /// <summary>
    /// Optional cost center or segment code for the target.
    /// </summary>
    [MaxLength(50)]
    public string? CostCenterCode { get; set; }
}
