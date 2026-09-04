namespace ErpSystem.Core.Interfaces.Reference;

/// <summary>
/// A module that stores <c>GeoAreaId</c> on one of its records, and can say how many of them point
/// at a given area. Implemented by each consumer; collected by the geography service so that
/// deleting an area someone still uses is refused.
/// </summary>
/// <remarks>
/// <para><b>⚠ Why this is inverted rather than a direct query.</b> Geography is shared reference
/// data and must not accumulate knowledge of every module that reads it (decision D-1) — HR is the
/// first consumer, with Location, CompanyProfile, the medical facilities and travel destinations
/// following in phase 4. A geography service that queried each of them by name would import half
/// the product to answer one question, and would have to be edited every time a new consumer
/// appeared. Each module registers its own probe instead.</para>
///
/// <para><b>⚠ Why a probe is needed at all, when there is a foreign key.</b> The FK only fires on a
/// HARD delete. Geography deletes are SOFT, so the constraint is never consulted: the row is merely
/// flagged, disappears from every read, and every record pointing at it silently loses its address
/// while keeping a dangling id. Measured 2026-09-03 — a seeded community with an employee living in
/// it deleted cleanly and took the address with it. The database cannot enforce this one; the
/// service has to.</para>
/// </remarks>
public interface IGeoAreaConsumer
{
    /// <summary>
    /// What the records are called, in the plural and in the words a user would recognise —
    /// "employees", "sites", "healthcare facilities". Used to build the refusal message.
    /// </summary>
    string ResourceName { get; }

    /// <summary>
    /// The same thing in the singular — "employee", "site", "healthcare facility".
    /// </summary>
    /// <remarks>
    /// ⚠ Stated, not derived. Trimming an "s" off the plural produced "1 healthcare facilitie" in
    /// a refusal a user was meant to act on. English pluralisation cannot be computed, and a
    /// message that tells someone their data is in use is the last place to be sloppy about it.
    /// </remarks>
    string ResourceNameSingular { get; }

    /// <summary>How many of this consumer's live records point at <paramref name="geoAreaId"/>.</summary>
    Task<int> CountUsagesAsync(Guid geoAreaId, Guid tenantId, CancellationToken ct = default);
}
