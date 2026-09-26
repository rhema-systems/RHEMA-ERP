namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// The month the current tenant's leave year begins (entitlement plan C1, half two).
/// </summary>
/// <remarks>
/// <para><b>Why this exists rather than each service reading the settings itself.</b> Six leave
/// services need the same number, in thirty-eight places. <c>ICompanyHrPolicyProvider</c> does not
/// cache — it queries on every call — so reading it per site would put a database round trip inside
/// loops that walk hundreds of balances. Caching it in each service would mean repeating the same
/// reasoning six times, and the fifth copy is where it drifts.</para>
///
/// <para>⚠ <b>Registered SCOPED, and that is the whole contract.</b> The value is read once per
/// request and no longer: a settings change takes effect on the very next call, and nothing holds a
/// tenant's number beyond the request that asked for it.</para>
///
/// <para>⚠ <b>Not for the background sweeps.</b> They loop over every tenant inside one scope, so a
/// per-scope cache would hand the second tenant the first tenant's leave year.
/// <c>LeaveReminderService</c> already loads each tenant's settings explicitly and reads
/// <c>LeaveYearStartMonth</c> from that — see its sweep.</para>
/// </remarks>
public interface ILeaveYearContext
{
    /// <summary>
    /// The current tenant's leave-year start month, 1–12. Falls back to January for a tenant with
    /// no settings row, which is what every tenant had before the setting existed.
    /// </summary>
    Task<int> StartMonthAsync(CancellationToken cancellationToken = default);
}
