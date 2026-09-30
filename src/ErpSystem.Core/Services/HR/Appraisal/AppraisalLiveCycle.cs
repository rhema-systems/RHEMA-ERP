using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// The live-cycle rule (performance closure E-d2b, D-43 and D-59): an appraisal's work is done while its cycle is
/// Open. A Draft cycle is being configured — nothing is generated on it and nobody works on it — and a Closed one is
/// finished. It covers every write on an appraisal or its children (the forms, nominations, HR's review and sign-off,
/// the return, HR's advance, the acknowledgment, responses, appeals, conversations, review events, the date
/// correction, the raw open, attachments), the employee's goals and the calibration panels. It leaves alone what ends
/// or undoes the work (a withdrawal, a removal, closing a Completed appraisal) and the records that outlive or precede
/// a running cycle (outcomes, PIPs, proposals, development plans, the journal, check-ins, unit and company goals).
/// </summary>
/// <remarks>
/// No write read the cycle's status: generation ran on Draft cycles by design, so UAT holds some twelve hundred
/// harness appraisals on cycles never opened, and a cycle closed before E-d2a could still be written to.
/// </remarks>
public static class AppraisalLiveCycle
{
    /// <summary>Whether work may be done on the cycle's appraisals: it is Open.</summary>
    public static bool IsLive(AppraisalCycleStatus status) => status == AppraisalCycleStatus.Open;

    /// <summary>
    /// Refuses <paramref name="action"/> unless the cycle is Open — an <see cref="InvalidOperationException"/>, which the
    /// controllers answer 422, naming the cycle and whether it has not been opened or is closed.
    /// </summary>
    public static void EnsureOpen(AppraisalCycleStatus status, string? cycleName, string action)
    {
        if (!IsLive(status))
            throw new InvalidOperationException(Refusal(status, cycleName, action));
    }

    /// <summary>"The self-evaluation cannot be saved: its cycle, Annual 2026, has not been opened."</summary>
    public static string Refusal(AppraisalCycleStatus status, string? cycleName, string action)
    {
        var cycle = string.IsNullOrWhiteSpace(cycleName) ? "its cycle" : $"its cycle, {cycleName},";
        return status == AppraisalCycleStatus.Closed
            ? $"{action}: {cycle} is closed."
            : $"{action}: {cycle} has not been opened — an appraisal's work is done while its cycle is open.";
    }

    /// <summary>
    /// Reads the appraisal's cycle in one projection and refuses <paramref name="action"/> unless it is Open. An unknown
    /// appraisal passes: the caller's own lookup answers it.
    /// </summary>
    public static async Task EnsureAppraisalCycleOpenAsync(
        IQueryable<PerformanceAppraisal> appraisals, Guid tenantId, Guid appraisalId, string action,
        CancellationToken cancellationToken)
    {
        var cycle = await appraisals
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Id == appraisalId)
            .Select(a => new { a.AppraisalCycle.Status, a.AppraisalCycle.CycleName })
            .FirstOrDefaultAsync(cancellationToken);
        if (cycle != null)
            EnsureOpen(cycle.Status, cycle.CycleName, action);
    }

    /// <summary>
    /// Reads the cycle and refuses <paramref name="action"/> unless it is Open. An unknown cycle, or another tenant's,
    /// is an <see cref="ArgumentException"/> (not found).
    /// </summary>
    public static async Task EnsureCycleOpenAsync(
        IQueryable<AppraisalCycle> cycles, Guid tenantId, Guid cycleId, string action,
        CancellationToken cancellationToken)
    {
        var cycle = await cycles
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Id == cycleId)
            .Select(c => new { c.Status, c.CycleName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Appraisal cycle '{cycleId}' was not found.");
        EnsureOpen(cycle.Status, cycle.CycleName, action);
    }
}
