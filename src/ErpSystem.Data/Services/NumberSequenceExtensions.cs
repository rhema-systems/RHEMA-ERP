using ErpSystem.Core.Interfaces;

namespace ErpSystem.Data.Services;

/// <summary>
/// Issues a reference number that is genuinely free, rather than merely the next one the counter
/// happens to hold.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> <see cref="INumberSequenceService"/> is the only writer that keeps
/// the <c>NumberSequences</c> counters moving, but it is NOT the only writer that puts numbers into
/// the tables those counters number. Seeders, data loads and repairs write rows directly, and every
/// one of them has to remember to call <see cref="INumberSequenceService.AdvanceToAtLeastAsync(string,long,int?,System.Threading.CancellationToken)"/>
/// afterwards. When one forgets — or, as measured on the demonstration database on 2026-09-15,
/// writes its rows successfully and then fails before it reaches its own flush — the counter is left
/// behind the data and the next record created through the real door dies on the unique index:</para>
/// <code>
/// Cannot insert duplicate key row in object 'dbo.JobVacancies' with unique index
/// 'IX_JobVacancy_Tenant_Number'. The duplicate key value is (…, VAC-000013).
/// </code>
/// <para>That surfaces as a 500 on a perfectly ordinary Create, and it does not clear by itself:
/// the counter is incremented in its own <c>SaveChanges</c>, so a failed create still burns the
/// number and the next attempt simply collides one higher.</para>
///
/// <para><b>What this does instead.</b> The number the counter hands out is checked against the
/// table before it is used. On the happy path that is one index seek and nothing else changes. On a
/// collision the counter is realigned to the table's own high-water mark in a single guarded update
/// — <c>AdvanceToAtLeastAsync</c> only ever moves a counter FORWARD — and one more number is drawn.
/// So a counter that has fallen behind repairs itself on first use, whatever put it there.</para>
///
/// <para>⚠ <paramref name="highestIssued"/> must parse the numbers already in the table rather than
/// take a string <c>MAX</c>: older loads wrote other shapes into the same column (the dev database
/// carries <c>VAC-REQ-2026-00001</c> alongside <c>VAC-000093</c>), and a string maximum picks the
/// wrong row and realigns the counter DOWNWARD to a number already in the register.</para>
/// </remarks>
public static class NumberSequenceExtensions
{
    /// <summary>Issues an unused number for the caller's own tenant.</summary>
    /// <param name="key">Sequence key and printed prefix, e.g. "VAC".</param>
    /// <param name="year">Year bucket, or <c>null</c> for counters that do not reset annually.</param>
    /// <param name="format">Turns a raw counter value into the printed number, e.g. <c>v => $"VAC-{v:D6}"</c>.</param>
    /// <param name="isTaken">True when the table already holds that number for this tenant.</param>
    /// <param name="highestIssued">The largest number already in the table for this tenant, parsed.</param>
    public static Task<string> NextUnusedAsync(
        this INumberSequenceService sequences,
        string key,
        int? year,
        Func<long, string> format,
        Func<string, Task<bool>> isTaken,
        Func<Task<long>> highestIssued,
        CancellationToken cancellationToken = default)
        => NextUnusedCoreAsync(
            key,
            () => sequences.NextAsync(key, year, cancellationToken),
            minimum => sequences.AdvanceToAtLeastAsync(key, minimum, year, cancellationToken),
            format, isTaken, highestIssued);

    /// <summary>
    /// Tenant-explicit overload, for the anonymous career portal — it supplies the tenant from the
    /// <c>X-Tenant-Id</c> header because there is no tenant claim to read.
    /// </summary>
    public static Task<string> NextUnusedAsync(
        this INumberSequenceService sequences,
        string key,
        Guid tenantId,
        int? year,
        Func<long, string> format,
        Func<string, Task<bool>> isTaken,
        Func<Task<long>> highestIssued,
        CancellationToken cancellationToken = default)
        => NextUnusedCoreAsync(
            key,
            () => sequences.NextAsync(key, tenantId, year, cancellationToken),
            minimum => sequences.AdvanceToAtLeastAsync(key, minimum, tenantId, year, cancellationToken),
            format, isTaken, highestIssued);

    private static async Task<string> NextUnusedCoreAsync(
        string key,
        Func<Task<long>> next,
        Func<long, Task<long>> advanceTo,
        Func<long, string> format,
        Func<string, Task<bool>> isTaken,
        Func<Task<long>> highestIssued)
    {
        var number = format(await next());
        if (!await isTaken(number)) return number;

        // The counter is behind the data. Realign it to what the table actually holds and draw again.
        // One retry is enough by construction: after the realignment the next value is above every
        // number in the table, so a second collision means something else owns the number space.
        await advanceTo(await highestIssued());

        var realigned = format(await next());
        if (!await isTaken(realigned)) return realigned;

        throw new InvalidOperationException(
            $"Could not issue an unused '{key}' number: both {number} and {realigned} are already in use "
            + "after the counter was realigned to the highest number in the table. Check for numbers "
            + "written in a shape the counter cannot parse.");
    }

    /// <summary>
    /// The counter value carried by a reference number — the digits after the last dash, so it reads
    /// both <c>VAC-000024</c> and <c>REQ-2026-00029</c>. Anything that does not parse is ignored
    /// rather than throwing: a hand-made reference number must not stop a create.
    /// </summary>
    public static long HighestIssued(IEnumerable<string?> numbers)
    {
        long max = 0;
        foreach (var n in numbers)
        {
            if (string.IsNullOrWhiteSpace(n)) continue;
            var tail = n[(n.LastIndexOf('-') + 1)..];
            if (long.TryParse(tail, out var value) && value > max) max = value;
        }
        return max;
    }
}
