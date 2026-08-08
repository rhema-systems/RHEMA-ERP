using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Per-tenant, per-document-type, per-year monotonic counter used to generate human-readable
/// reference numbers (e.g. NOM-2025-001) without the race / cross-tenant-collision / soft-delete-reuse
/// defects of the old <c>Count()+1</c> approach. Increment is done transactionally with an optimistic
/// concurrency token (<see cref="RowVersion"/>) and a retry loop in the number-sequence service.
/// </summary>
public class NumberSequence : TenantEntity
{
    /// <summary>Document-type key / prefix, e.g. "NOM", "SCHED", "TREQ", "TPLAN", "CERT".</summary>
    [Required]
    [MaxLength(50)]
    public string SequenceKey { get; set; } = string.Empty;

    /// <summary>Calendar year the counter belongs to (counters reset per year).</summary>
    public int Year { get; set; }

    /// <summary>Last value handed out. The next number is this value once incremented.</summary>
    public long NextValue { get; set; }

    /// <summary>Optimistic concurrency token so concurrent increments don't silently clobber each other.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
