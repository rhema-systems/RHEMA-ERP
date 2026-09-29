using ErpSystem.Core.Entities.HR.StaffLeave;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// How many days of annual leave are owed to each of some people at a date — built up and not yet
/// taken (round 5, lane C6; shared with the leaver's settlement in lane L2).
/// </summary>
/// <remarks>
/// <para><b>One working, two readers.</b> The <i>leave owed</i> report (Finance's figure for its
/// books, everybody on the books at a date) and a leaver's final settlement (one person, at their
/// last day) ask the same question. Before lane L the settlement summed every balance of every leave
/// type and every year instead — so the two could never agree.</para>
///
/// <para><b>Owed</b> = built up by the date (the accrual working, which stops at the day somebody
/// leaves when the policy pro-rates on exit) + carried in (in full until the carry-over lapses; then
/// only the carried days taken in time) + adjustments − taken by the date − cashed in. Leave booked
/// after the date and requests awaiting a decision are returned beside it, not taken from it: what a
/// reader does with them is the reader's question.</para>
/// </remarks>
public interface ILeaveOwedCalculator
{
    /// <summary>
    /// The figures for each of <paramref name="people"/> as at <paramref name="asOf"/>, in the leave
    /// year that date falls in, on the tenant's one active Annual leave type.
    /// </summary>
    /// <param name="accrueToYearEnd">
    /// Work the build-up to the END of the leave year instead of to <paramref name="asOf"/> — for a
    /// leaver, whose last day is on their subject. The accrual policy's <c>ProRateOnExit</c> then
    /// decides: on, the clock stops at the last day; off, the leaver is credited the whole year.
    /// ⚠ Asked as at the last day instead, the clock would stop there whatever the policy said, and
    /// the setting would bind nothing (round 5, lane L2). Taken days and the carry-over lapse are
    /// still read as at <paramref name="asOf"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">No active leave type is of the Annual kind.</exception>
    Task<LeaveOwedComputation> ComputeAsync(
        DateOnly asOf, IReadOnlyList<LeaveAccrualSubject> people, bool accrueToYearEnd = false,
        CancellationToken ct = default);
}

/// <summary>The leave year and type the figures are for, and the figures per employee.</summary>
public sealed class LeaveOwedComputation
{
    public required LeaveType AnnualType { get; init; }
    public required int Year { get; init; }
    public required DateOnly YearStart { get; init; }
    public required DateOnly YearEnd { get; init; }

    /// <summary>The last day carried-in days count in full, when the type lets them lapse.</summary>
    public DateOnly? CarryOverExpiresOn { get; init; }

    public required IReadOnlyDictionary<Guid, LeaveOwedFigures> ByEmployee { get; init; }
}

/// <summary>
/// One person's figures. <see cref="OwedDays"/> is the answer; the rest show how.
/// <see cref="BuiltUpTo"/> is the day the build-up was worked to (null where the type does not
/// accrue and the whole entitlement stands).
/// </summary>
public sealed record LeaveOwedFigures(
    decimal EntitledDays,
    DateOnly? BuiltUpTo,
    decimal BuiltUpDays,
    decimal CarriedInDays,
    decimal AdjustmentDays,
    decimal TakenDays,
    decimal CashedInDays,
    decimal OwedDays,
    decimal BookedDays,
    decimal AwaitingApprovalDays);
