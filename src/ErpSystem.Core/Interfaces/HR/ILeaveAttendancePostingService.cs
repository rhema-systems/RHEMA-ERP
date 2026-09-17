using ErpSystem.Core.Entities.HR.StaffLeave;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Writes approved leave onto the attendance register, and takes it off again.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> <c>StaffDailyAttendance.LeaveRequestId</c> and
/// <c>StaffAttendanceStatus.OnLeave</c> have both existed since the port and nothing ever wrote
/// either. So approving leave marked no attendance day: <c>DaysOnLeave</c> on the monthly summary —
/// which the <b>payroll export</b> reads — stayed zero unless a clerk hand-edited every day, and
/// <c>LeaveRequest.AttendanceDays</c> was permanently empty. The closure plan calls it the largest
/// structural gap in the leave module (L-27).</para>
///
/// <para><b>It takes the chargeable days rather than working them out.</b> The caller already has
/// that list — it is what the request's <c>TotalDays</c> counts — so passing it in is what
/// guarantees the number of <c>OnLeave</c> days and the request's own total can never disagree.
/// Two independent walks over "weekends and holidays per leave type" would drift the first time
/// either was edited.</para>
/// </remarks>
public interface ILeaveAttendancePostingService
{
    /// <summary>
    /// Marks each chargeable day of an approved request as <c>OnLeave</c>, carrying the request id.
    /// Idempotent: posting the same request twice does not double-write.
    /// </summary>
    /// <returns>What was written, and what was deliberately left alone.</returns>
    /// <remarks>
    /// ⚠ <paramref name="tenantId"/> is explicit rather than read from the ambient user context.
    /// The nightly reconciliation calls this from a background service, where there is no signed-in
    /// user and no tenant to infer — a service that only works inside a request is a service that
    /// silently does nothing on a timer.
    /// </remarks>
    Task<LeaveAttendancePostingResult> PostAsync(
        LeaveRequest request, Guid tenantId, IReadOnlyList<DateOnly> chargeableDays,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the attendance days this request put there, when it is cancelled or its dates move.
    /// </summary>
    /// <remarks>
    /// Only rows this service could have created are removed — see the implementation's note on why
    /// that set is exactly "carries this request id, and nobody has punched or written on it".
    /// </remarks>
    Task<int> ReverseAsync(Guid leaveRequestId, Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// The outcome of posting leave onto the attendance register.
/// </summary>
/// <param name="DaysWritten">Days now marked <c>OnLeave</c> against this request.</param>
/// <param name="DaysSkipped">
/// Chargeable days left untouched because the attendance register already had something real for
/// them — a punch, or a clerk's own note. Leave never overwrites an observation; a non-zero value
/// here is worth surfacing rather than swallowing, because those days will not reach the payroll
/// export as leave.
/// </param>
public sealed record LeaveAttendancePostingResult(int DaysWritten, int DaysSkipped);
