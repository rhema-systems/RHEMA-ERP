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
}
