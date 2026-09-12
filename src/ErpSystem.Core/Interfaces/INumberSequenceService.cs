namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Generates unique, human-readable reference numbers (e.g. NOM-2025-001) using a per-tenant,
/// per-key, per-year sequence table. Safe under concurrency and immune to soft-delete reuse —
/// replaces the old <c>Count()+1</c> pattern.
/// </summary>
public interface INumberSequenceService
{
    /// <summary>
    /// Returns the next reference number for the given <paramref name="key"/> (used as both the
    /// sequence key and the printed prefix), formatted as <c>{key}-{year}-{seq:D3}</c> for the
    /// current UTC year. Atomic and unique per tenant.
    /// </summary>
    /// <param name="key">Document-type prefix, e.g. "NOM", "SCHED", "TREQ", "TPLAN", "CERT".</param>
    Task<string> GenerateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next raw sequence value for <paramref name="key"/>, leaving formatting to the
    /// caller. Use this where a document already has an established number format that must be
    /// preserved (e.g. <c>APP-0000001</c>, <c>REQ-2026-00007</c>).
    /// </summary>
    /// <param name="key">Sequence key, e.g. "REQ", "APP", "CAND", "VAC".</param>
    /// <param name="year">
    /// Year bucket the counter belongs to. Pass the year for numbers that reset annually and print the
    /// year (e.g. <c>REQ-2026-…</c>). Pass <c>null</c> for numbers with no year in them
    /// (e.g. <c>APP-0000001</c>) — a year-scoped counter would restart every January and collide with
    /// the previous year's numbers.
    /// </param>
    Task<long> NextAsync(string key, int? year = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant-explicit overload for anonymous callers (public career portal), where there is no
    /// authenticated tenant claim to resolve. Without this, the sequence row would be stamped with
    /// <see cref="Guid.Empty"/> and violate the NumberSequences → Tenants foreign key.
    /// </summary>
    Task<long> NextAsync(string key, Guid tenantId, int? year = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises the counter for <paramref name="key"/> so the next number issued is greater than
    /// <paramref name="minimum"/>, and returns the watermark it now stands at.
    /// </summary>
    /// <remarks>
    /// <para>For DATA LOADS. Numbers that already exist were not issued by this counter — they were
    /// loaded, seeded or migrated — so the counter knows nothing about them and would reissue them
    /// one by one. This is how a load tells it what has already been used.</para>
    ///
    /// <para>⚠ It only ever moves the counter FORWARD. A minimum below the current watermark is a
    /// no-op, because lowering a counter hands out numbers that are already in the register — the
    /// exact failure this method exists to prevent.</para>
    ///
    /// <para>Written as one guarded update rather than a loop of <see cref="NextAsync"/> calls: a
    /// register loaded with 8,000 staff would otherwise cost 8,000 round trips, and a loop with any
    /// step limit gives up silently on the loads that need it most.</para>
    /// </remarks>
    Task<long> AdvanceToAtLeastAsync(
        string key, long minimum, int? year = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Where the counter stands, without moving it. <c>0</c> when it has never issued a number.
    /// </summary>
    /// <remarks>
    /// The watermark, i.e. the value LAST issued — the next number out will be one higher. Exists so
    /// a settings screen can say what the counter will do before somebody finds out by hiring.
    /// </remarks>
    Task<long> PeekAsync(string key, int? year = null, CancellationToken cancellationToken = default);
}
