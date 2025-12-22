using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents an account for tracking non-financial quantities.
/// Supports hierarchical structure (parent/child) for rollup reporting.
/// Similar to Chart of Accounts but for quantities like headcount, square footage, hours.
/// </summary>
public class UnitAccount : TenantEntity
{
    // ========================================================================
    // ACCOUNT IDENTIFICATION
    // ========================================================================

    /// <summary>
    /// Unique account number within the tenant (e.g., "U-1000", "U-1100").
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the unit account.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this account tracks.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    // ========================================================================
    // UNIT TYPE RELATIONSHIP
    // ========================================================================

    /// <summary>
    /// Foreign key to the unit type that defines what this account measures.
    /// </summary>
    [Required]
    public Guid UnitTypeId { get; set; }

    /// <summary>
    /// Navigation property to the unit type.
    /// </summary>
    [ForeignKey(nameof(UnitTypeId))]
    public virtual UnitType? UnitType { get; set; }

    // ========================================================================
    // HIERARCHY STRUCTURE
    // ========================================================================

    /// <summary>
    /// Foreign key to parent account for hierarchical structure.
    /// Null for top-level accounts.
    /// </summary>
    public Guid? ParentAccountId { get; set; }

    /// <summary>
    /// Navigation property to parent account.
    /// </summary>
    [ForeignKey(nameof(ParentAccountId))]
    public virtual UnitAccount? ParentAccount { get; set; }

    /// <summary>
    /// Child accounts under this account.
    /// </summary>
    public virtual ICollection<UnitAccount> ChildAccounts { get; set; } = new List<UnitAccount>();

    /// <summary>
    /// Level in the account hierarchy (1 = top level).
    /// Calculated based on parent chain depth.
    /// </summary>
    [Required]
    public int AccountLevel { get; set; } = 1;

    // ========================================================================
    // ACCOUNT TYPE AND STATUS
    // ========================================================================

    /// <summary>
    /// Whether this account can receive direct journal entry postings.
    /// False = summary/rollup account that aggregates child balances.
    /// </summary>
    [Required]
    public bool IsPostingAccount { get; set; } = true;

    /// <summary>
    /// Whether this account is active. Inactive accounts cannot receive postings.
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Current balance for quick reference (denormalized).
    /// Updated when journal entries are posted.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal CurrentBalance { get; set; } = 0;

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    /// <summary>
    /// Journal entry lines that affect this account.
    /// </summary>
    public virtual ICollection<UnitJournalEntryLine> JournalEntryLines { get; set; } = new List<UnitJournalEntryLine>();

    /// <summary>
    /// Period balances for this account.
    /// </summary>
    public virtual ICollection<UnitAccountBalance> Balances { get; set; } = new List<UnitAccountBalance>();
}
