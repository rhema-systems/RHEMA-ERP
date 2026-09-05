using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The staff-travel reminder sweep: the thing that finally reads the expiry endpoints nobody was
/// reading.
/// </summary>
/// <remarks>
/// Travel is full of dates that matter and nothing was watching any of them. The endpoints existed
/// — <c>compliance/documents/expiring</c>, <c>compliance/visa-applications/expiring</c>,
/// <c>finance/advances/overdue-settlements</c>, <c>requests/upcoming</c> — and returned their rows
/// to nobody at all. Three other HR areas already had a sweep; travel did not.
/// </remarks>
public interface IStaffTravelReminderService
{
    Task<StaffTravelReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What a sweep run at <paramref name="asOf"/> would fire. Reads only — claims nothing, sends
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The <c>asOf</c> seam exists because every date in this engine is server-stamped, so without
    /// it a test can only ever assert what happens to be true today. Area 9 learned that the hard
    /// way and the same seam is provided here.
    /// </remarks>
    Task<IEnumerable<StaffTravelReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default);

    Task<IEnumerable<StaffTravelReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default);

    Task<IEnumerable<StaffTravelReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default);
}
