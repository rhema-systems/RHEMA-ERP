using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The asset reminder sweep — area 16, slice 9, AST-1.
/// </summary>
/// <remarks>
/// <para>AST-1 asks for a way to <i>monitor</i> maintenance, and slice 9's three reads are only
/// half of that: a list nobody opens is not monitoring. The sweep is the half that goes and finds
/// somebody. It is the sixth HR engine of this shape, and deliberately identical to the other five
/// — one scoped service, a daily host and a run-now endpoint sharing it, a dedupe key per item, and
/// an <c>asOf</c> preview.</para>
///
/// <para>Slice 9 gives it one subject, maintenance, in three rungs: due soon, overdue, and
/// requiring regular maintenance with no schedule at all. Slice 11 adds insurance expiry and
/// overdue returns to the same engine rather than starting another.</para>
/// </remarks>
public interface IAssetReminderService
{
    Task<AssetReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What a sweep run at <paramref name="asOf"/> would fire. Reads only — claims nothing, sends
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The <c>asOf</c> seam exists because a maintenance schedule is written by the server from the
    /// asset's interval, so a test that cannot move the date can only ever assert what happens to
    /// be true today — and the overdue rungs, which are the ones that matter, are unreachable
    /// inside a single run. Area 9 learned that the hard way and the same seam is provided here.
    /// It claims no dedupe key, so previewing a future date cannot rob the real sweep of a reminder.
    /// </remarks>
    Task<IEnumerable<AssetReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default);

    Task<IEnumerable<AssetReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default);

    Task<IEnumerable<AssetReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default);
}
