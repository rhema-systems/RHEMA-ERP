using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Watches the dates leave carries and chases each one, once.
/// </summary>
/// <remarks>
/// <para>The twelfth of HR's reminder engines and the last to be built, for the module with more
/// dates that matter than any of the others. Before it, nothing was raised when leave was about to
/// start, when approved leave ran past its end date and was never closed, when mandatory leave went
/// untaken, when carry-over was about to expire, or when a request sat undecided (closure plan
/// R-6 / R-10 / L-23).</para>
///
/// <para><b>⚠ What this engine deliberately does NOT do.</b> It never moves a balance. Carry-over
/// and forfeiture are <c>LeaveYearEndService</c>'s, and that service is deliberately not hosted
/// because those two acts change people's entitlements and automating them is TDC's policy call
/// (closure ledger; the system guide's L-25 calls it a gap and is wrong). This engine only tells
/// somebody that a date is coming — which is exactly why it CAN be scheduled when the year-end
/// cannot.</para>
/// </remarks>
public interface ILeaveReminderService
{
    Task<LeaveReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What a sweep run at <paramref name="asOf"/> would fire. Reads only — claims nothing, sends
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The <c>asOf</c> seam exists because every date in this engine is server-stamped, so without
    /// it a test can only ever assert what happens to be true today. Every other HR engine carries
    /// the same seam for the same reason.
    /// </remarks>
    Task<IEnumerable<LeaveReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default);

    Task<IEnumerable<LeaveReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default);

    Task<IEnumerable<LeaveReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default);
}
