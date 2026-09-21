using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Reference;

/// <summary>
/// The one place a record's free-text City and Region are written from its <c>GeoAreaId</c>, and
/// the one place the area is checked against the country stored beside it.
/// </summary>
/// <remarks>
/// <para><b>Why a shared helper.</b> Round 2 lane D2 added a <c>GeoAreaId</c> to four more tables.
/// Before it there were already four private copies of <c>ApplyGeoAreaSnapshotAsync</c> —
/// <c>EmployeeService</c>, <c>CompanyProfileService</c>, <c>LocationStructureServices</c>,
/// <c>MedicalServices</c> — and they had already drifted: two wrote the region and two silently
/// dropped it. Eight copies of a rule is eight chances for the ninth to be wrong, and the
/// geography module exists precisely to end that kind of drift.</para>
///
/// <para><b>The rules, in one place.</b></para>
/// <list type="number">
///   <item><description><b>A null area changes nothing.</b> Most records predate the tree and the
///   free text is the only address they have; blanking it would destroy it.</description></item>
///   <item><description><b>The tree wins.</b> A caller sending both an area and its own spelling of
///   the city has the tree's spelling written over it — otherwise the two drift and nobody can say
///   which is right.</description></item>
///   <item><description><b>An unreadable area changes nothing.</b> <c>(null, null)</c> comes back
///   for another tenant's area, or one deleted between the form loading and the save. The record
///   keeps what it said, and the miss is logged rather than swallowed.</description></item>
///   <item><description><b>The country must agree, and is filled in when silent.</b> A record that
///   stores both cannot say Nigeria while pointing at a Ghanaian district — the snapshots written
///   from the area would then contradict the country on the same row.</description></item>
/// </list>
///
/// <para><b>⚠ This does not protect the area from deletion.</b> Geography deletes are SOFT, so the
/// foreign key never fires. Every table that gains the column also needs an
/// <c>IGeoAreaConsumer</c> probe registered — see <c>GeoAreaConsumers.cs</c>.</para>
/// </remarks>
public static class GeoAddressSnapshot
{
    /// <summary>
    /// Writes the region and city snapshots for <paramref name="geoAreaId"/> through the setters
    /// the caller supplies, leaving both untouched when there is nothing trustworthy to write.
    /// </summary>
    /// <param name="setRegion">
    /// Where the tier-1 name goes — <c>State</c> on the employee, <c>Region</c> everywhere else.
    /// Pass <c>null</c> for a record that has no region column, which is why this parameter is
    /// nullable rather than the callers each keeping their own trimmed copy of the rule.
    /// </param>
    /// <param name="subject">
    /// What the record is, in the words a log reader would recognise — "employee contact",
    /// "guarantor". It only ever reaches a warning line.
    /// </param>
    public static async Task ApplyAsync(
        IGeographyService geography,
        ILogger logger,
        Guid? geoAreaId,
        Action<string>? setRegion,
        Action<string>? setCity,
        string subject,
        Guid recordId,
        CancellationToken ct = default)
    {
        if (geoAreaId is not { } areaId) return;

        var (region, city) = await geography.GetAddressSnapshotAsync(areaId, ct);

        if (region is null && city is null)
        {
            logger.LogWarning(
                "{Subject} {RecordId} references geo area {GeoAreaId}, which could not be resolved; "
                + "the address snapshot was left unchanged.", subject, recordId, areaId);
            return;
        }

        if (region is not null) setRegion?.Invoke(region);
        if (city is not null) setCity?.Invoke(city);
    }

    /// <summary>
    /// Reconciles a record's <c>CountryId</c> with the country its <c>GeoAreaId</c> implies:
    /// returns the country to store, and throws when the two contradict each other.
    /// </summary>
    /// <remarks>
    /// <para>Silence is not a contradiction — a record with an area and no country is given the
    /// area's country rather than being refused, because the country is derivable and asking the
    /// user to state it twice is how the two come to disagree in the first place.</para>
    ///
    /// <para>An area that cannot be read leaves the country alone: the snapshot call above has
    /// already logged the miss, and refusing here would turn a stale id into an unsaveable form.</para>
    /// </remarks>
    /// <returns>The <c>CountryId</c> the record should carry.</returns>
    /// <exception cref="InvalidOperationException">The stated country is not the area's.</exception>
    public static async Task<Guid?> ReconcileCountryAsync(
        IGeographyService geography,
        Guid? geoAreaId,
        Guid? statedCountryId,
        string subject,
        CancellationToken ct = default)
    {
        if (geoAreaId is not { } areaId) return statedCountryId;

        var areaCountryId = await geography.GetCountryForAreaAsync(areaId, ct);
        if (areaCountryId is null) return statedCountryId;

        if (statedCountryId is null) return areaCountryId;

        if (statedCountryId != areaCountryId)
            throw new InvalidOperationException(
                $"The area chosen for this {subject} is not in the country stated on it. "
                + "Pick an area within the chosen country, or change the country to match.");

        return statedCountryId;
    }
}
