using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a line item within a unit journal entry.
/// Each line posts a quantity to a specific unit account.
/// </summary>
public class UnitJournalEntryLine : TenantEntity
{
    // ========================================================================
    // PARENT ENTRY RELATIONSHIP
    // ========================================================================

    /// <summary>
    /// Foreign key to the parent journal entry.
    /// </summary>
    [Required]
    public Guid UnitJournalEntryId { get; set; }

    /// <summary>
    /// Navigation property to the parent journal entry.
    /// </summary>
    [ForeignKey(nameof(UnitJournalEntryId))]
    public virtual UnitJournalEntry? UnitJournalEntry { get; set; }

    /// <summary>
    /// Line number within the entry (for ordering).
    /// </summary>
    [Required]
    public int LineNumber { get; set; }

    // ========================================================================
    // ACCOUNT AND QUANTITY
    // ========================================================================

    /// <summary>
    /// Foreign key to the unit account being affected.
    /// </summary>
    [Required]
    public Guid UnitAccountId { get; set; }

    /// <summary>
    /// Navigation property to the unit account.
    /// </summary>
    [ForeignKey(nameof(UnitAccountId))]
    public virtual UnitAccount? UnitAccount { get; set; }

    /// <summary>
    /// Quantity to post to the account.
    /// Positive = increase, Negative = decrease.
    /// Precision up to 6 decimal places.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Optional description for this line.
    /// </summary>
    [MaxLength(300)]
    public string? Description { get; set; }
}
