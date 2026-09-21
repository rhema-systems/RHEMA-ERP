using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The nightly sweep that chases what a team is letting slip.
/// </summary>
/// <remarks>
/// <para>Round 2, lane F2 (plan § 6.6.2). Five rules: a task due within the tenant's lead time, a
/// task already overdue, an objective past its due date, a meeting happening tomorrow, and terms of
/// reference lapsing within thirty days.</para>
///
/// <para><b>⚠ The sweep logic is SCOPED, not baked into the background service.</b> The HR-gated
/// run-now endpoint exercises exactly the code path the host does — the split every other HR engine
/// uses. A sweep only reachable from a timer cannot be tested, and this codebase has already found
/// <b>two HR sweeps that had never run at all</b> because the host registration was missing and
/// nothing said so.</para>
///
/// <para><b>Send-once.</b> A dedupe key encodes the item, the kind and the DUE date, claimed in the
/// same <c>SaveChanges</c> that records the run — so the nightly host and the run-now button cannot
/// double-send even if they overlap. Moving a task's due date changes the key and re-arms the
/// reminder, which is what a moved deadline should do.</para>
/// </remarks>
public interface ITeamReminderService
{
    /// <summary>
    /// Runs every rule for one tenant and records what it dispatched.
    /// </summary>
    /// <param name="trigger">"Scheduled" from the host, "Manual" from the run-now endpoint.</param>
    Task<TeamReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>The recent runs, newest first — how a person answers "did it fire last night?".</summary>
    Task<IEnumerable<TeamReminderRunDto>> GetRunsAsync(int take = 20, CancellationToken cancellationToken = default);

    /// <summary>What one run actually sent.</summary>
    Task<IEnumerable<TeamReminderDispatchDto>> GetDispatchesAsync(
        Guid runId, CancellationToken cancellationToken = default);
}
