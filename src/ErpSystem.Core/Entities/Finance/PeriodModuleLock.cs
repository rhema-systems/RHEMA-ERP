using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Period Module Lock entity - junction table for module-level period locking.
/// Enables locking a fiscal period for specific modules while keeping it open for others.
/// Implements Finance-first hierarchy: Finance lock blocks all until explicitly unlocked.
/// </summary>
public class PeriodModuleLock : BaseEntity
{
    // ========================================================================
    // TENANT SCOPE
    // ========================================================================

    /// <summary>
    /// Tenant Identifier.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    // ========================================================================
    // FOREIGN KEYS
    // ========================================================================

    /// <summary>
    /// Reference to the fiscal period being locked.
    /// </summary>
    [Required]
    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// Reference to the module this lock applies to.
    /// </summary>
    [Required]
    public Guid ModuleDefinitionId { get; set; }

    // ========================================================================
    // LOCK STATUS
    // ========================================================================

    /// <summary>
    /// Whether the period is locked for this specific module.
    /// TRUE = no transactions allowed for this module in this period.
    /// FALSE = transactions allowed (unless global lock or Finance hierarchy).
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Date when this module-specific lock was applied.
    /// </summary>
    public DateTime? LockedDate { get; set; }

    /// <summary>
    /// User who locked the period for this module.
    /// </summary>
    public Guid? LockedByUserId { get; set; }

    /// <summary>
    /// Reason for locking the period for this module.
    /// </summary>
    [MaxLength(500)]
    public string? LockReason { get; set; }

    /// <summary>
    /// Date when this module was explicitly unlocked (if ever).
    /// Tracks when admin opens a module while Finance is still locked.
    /// </summary>
    public DateTime? UnlockedDate { get; set; }

    /// <summary>
    /// User who unlocked this module in the period.
    /// </summary>
    public Guid? UnlockedByUserId { get; set; }

    /// <summary>
    /// Reason for unlocking (required for audit trail).
    /// </summary>
    [MaxLength(500)]
    public string? UnlockReason { get; set; }

    /// <summary>
    /// UTC instant when this temporary module reopening expires. An expired reopening
    /// is treated as locked by the posting engine even before background reconciliation.
    /// </summary>
    public DateTime? ReopenExpiresAtUtc { get; set; }

    /// <summary>
    /// UTC instant when the automatic expiry warning was sent to the administrator.
    /// </summary>
    public DateTime? ExpiryWarningSentAtUtc { get; set; }

    /// <summary>
    /// UTC instant when the temporary reopening was automatically relocked.
    /// </summary>
    public DateTime? AutoRelockedDate { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    /// <summary>
    /// The fiscal period this lock applies to.
    /// </summary>
    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;

    /// <summary>
    /// The module this lock applies to.
    /// </summary>
    [ForeignKey(nameof(ModuleDefinitionId))]
    public virtual ModuleDefinition ModuleDefinition { get; set; } = null!;
}
