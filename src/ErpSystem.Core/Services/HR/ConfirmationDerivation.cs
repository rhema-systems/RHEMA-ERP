namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The rule for a confirmation date nobody supplied (HR finish plan lane 11): the hire date plus the
/// probation term, for somebody whose term had ended before they were entered.
/// </summary>
/// <remarks>
/// <para>Decided by the user on 2026-09-25 ("derive and mark"). Measured that day on UAT: 2,191 of
/// 2,399 employees on probation, 1,454 of them hired five or more years earlier, because the import
/// carried no confirmation date and the hire path opens a probation for every permanent employee who
/// arrives without one. This is what an import carrying the date would have produced.</para>
///
/// <para>⚠ One rule for both doors — the import, for a row whose date is blank, and the repair, for
/// the employees already entered — so the two can never disagree about who was confirmed when.</para>
/// </remarks>
public static class ConfirmationDerivation
{
    /// <summary>
    /// A term in days as a probation record counts it: months, at least one. A term of a few days is
    /// still a probation, and a zero-month record would end the day it began.
    /// </summary>
    public static int TermMonths(int probationDays) => Math.Max(1, (int)Math.Round(probationDays / 30.0));

    /// <summary>When a term of <paramref name="probationDays"/> that began on <paramref name="hired"/> ends.</summary>
    public static DateOnly TermEnd(DateOnly hired, int probationDays) => hired.AddMonths(TermMonths(probationDays));

    /// <summary>
    /// The derived confirmation date — the day the term ended — or null when it had not ended
    /// before <paramref name="enteredOn"/>.
    /// </summary>
    /// <remarks>
    /// Null is the honest answer for both of the other cases: somebody whose term is still running
    /// is on probation, and somebody whose term ended after they were entered is due for
    /// confirmation through the ordinary process, against an authority and a letter.
    /// </remarks>
    public static DateOnly? Derive(DateOnly termEnd, DateOnly enteredOn) => termEnd < enteredOn ? termEnd : null;
}
