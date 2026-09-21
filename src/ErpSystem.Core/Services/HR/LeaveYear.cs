namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Where a leave year starts and ends, and which leave year a date falls in.
/// </summary>
/// <remarks>
/// <para><b>Entitlement plan C1, half one.</b> Every leave service computed its own year boundaries
/// inline — <c>new DateOnly(year, 1, 1)</c> and <c>StartDate.Year</c>, in thirty places across six
/// services — which made "the leave year is the calendar year" an assumption spread too thin to
/// change. This is the one place that answers the question.</para>
///
/// <para>⚠ <b>Half one deliberately changes NOTHING.</b> Every caller passes
/// <see cref="CalendarStartMonth"/>, for which these three methods reduce exactly to the arithmetic
/// they replaced. The existing harness suite is the proof of that, which is the entire reason the
/// work was split: the risk in this change is the thirty call sites, not the setting, so the sites
/// move first while the answer is still known.</para>
///
/// <para><b>Half two</b> replaces the constant with the tenant's <c>LeaveYearStartMonth</c> and
/// touches nothing else.</para>
///
/// <para>⚠ <b>A leave year is LABELLED by the calendar year it starts in</b> — with an April start,
/// 15 March 2028 is in leave year 2027 (April 2027 – March 2028). That matches
/// <see cref="HrFiscalYear"/>, which labels fiscal years the same way, and it is the only convention
/// under which <c>LeaveBalance.Year</c> can stay an <c>int</c>.</para>
///
/// <para>⚠ <b>And that <c>int</c> is why this file is small.</b> A stored <c>Year</c> column on a
/// balance, an adjustment, an encashment or a plan is a LABEL, not a date — those comparisons are
/// unaffected by where the year starts and were deliberately left alone. Only dates being mapped to
/// a year, and the boundaries of a year, go through here.</para>
/// </remarks>
public static class LeaveYear
{
    /// <summary>
    /// January. ⚠ Passed by every caller in half one, so the helpers reduce to the inline
    /// arithmetic they replaced and the refactor is provably behaviour-preserving.
    /// </summary>
    public const int CalendarStartMonth = 1;

    /// <summary>
    /// Which leave year <paramref name="date"/> falls in, labelled by the calendar year the leave
    /// year starts in.
    /// </summary>
    /// <remarks>
    /// ⚠ An out-of-range month is treated as January rather than throwing. A leave year is asked
    /// for on read paths all over this module, and a settings row with a bad value should degrade to
    /// today's behaviour rather than take the module down — the same defence
    /// <see cref="HrFiscalYear"/> makes.
    /// </remarks>
    public static int For(DateOnly date, int startMonth)
    {
        if (startMonth <= 1 || startMonth > 12) return date.Year;
        return date.Month >= startMonth ? date.Year : date.Year - 1;
    }

    /// <inheritdoc cref="For(DateOnly, int)"/>
    public static int For(DateTime date, int startMonth) => For(DateOnly.FromDateTime(date), startMonth);

    /// <summary>The first day of leave year <paramref name="year"/>.</summary>
    public static DateOnly StartOf(int year, int startMonth)
        => new(year, startMonth <= 1 || startMonth > 12 ? 1 : startMonth, 1);

    /// <summary>
    /// The last day of leave year <paramref name="year"/>.
    /// </summary>
    /// <remarks>
    /// Derived from the start rather than written out, so a February start lands on the last day of
    /// the following January without anybody reasoning about month lengths or leap years.
    /// </remarks>
    public static DateOnly EndOf(int year, int startMonth)
        => StartOf(year, startMonth).AddYears(1).AddDays(-1);
}
