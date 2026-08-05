using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Builds the Attendance &amp; Time dashboard payload in a single request.
///
/// <see cref="AttendanceDashboardDto"/> has existed in the DTOs since the module was ported
/// but nothing implemented it, so the dashboard screen had to assemble its tiles from a
/// handful of narrow list endpoints and count them client-side. That works but it ships whole
/// collections just to take their length, and it cannot express the trend or the
/// chronic-absentee ranking at all. This computes the lot server-side.
/// </summary>
public interface IAttendanceDashboardService
{
    /// <summary>
    /// Aggregates today's snapshot, the open pay period, the pending approval queues, alert
    /// counts, a short attendance trend and the chronic-absentee list.
    /// </summary>
    /// <param name="asOf">
    /// The day treated as "today". Defaults to the current UTC date. Supplied mainly so the
    /// figures can be reproduced for a past day when someone questions them.
    /// </param>
    /// <param name="trendDays">How many days the daily trend covers, ending at <paramref name="asOf"/>.</param>
    /// <param name="riskListSize">How many chronic absentees to return.</param>
    Task<AttendanceDashboardDto> GetDashboardAsync(
        DateOnly? asOf = null,
        int trendDays = 7,
        int riskListSize = 5,
        CancellationToken ct = default);
}
