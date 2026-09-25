namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// One employee's use of a leave type in one leave year, as at a date.
/// </summary>
/// <param name="TakenThrough">
/// Days of approved, in-progress or completed leave falling on or before the date, counted by the walk
/// that charged them — so leave straddling the date counts only its days up to it. Leave booked
/// before the date counts even if the date is still ahead: it will have been used by then.
/// </param>
/// <param name="TakenOrBooked">The whole of that leave, whatever its dates.</param>
/// <param name="Pending">Days awaiting approval.</param>
public sealed record LeaveUsage(decimal TakenThrough, decimal TakenOrBooked, decimal Pending);

/// <summary>
/// How much of a leave type employees have used by a date (round 5, lane G).
/// </summary>
/// <remarks>
/// <para><b>The one answer to "which of these days had been used by then".</b> Carried-over days
/// lapse unless they are taken in time, and three readers ask about that: the year-end expiry run
/// (what to remove), reminder sweep 5 (what to warn about), and the "leave owed" report (what still
/// counts). They share this, so the run removes exactly the days the reminder warned about and the
/// report counts the same ones.</para>
///
/// <para>A request belongs to the leave year its first day falls in, as everywhere in leave. The tenant
/// is passed explicitly, because the reminder sweep runs with no user.</para>
/// </remarks>
public interface ILeaveUsageReader
{
    /// <summary>
    /// Per employee, the use of <paramref name="leaveTypeId"/> in the leave year
    /// <paramref name="yearStart"/>–<paramref name="yearEnd"/>, as at <paramref name="through"/>.
    /// Employees with no leave of the type in the year are absent.
    /// </summary>
    /// <param name="employeeIds">Only these employees; <c>null</c> for everybody.</param>
    Task<IReadOnlyDictionary<Guid, LeaveUsage>> ReadAsync(
        Guid tenantId, Guid leaveTypeId, DateOnly yearStart, DateOnly yearEnd, DateOnly through,
        IReadOnlyCollection<Guid>? employeeIds = null, CancellationToken ct = default);
}
