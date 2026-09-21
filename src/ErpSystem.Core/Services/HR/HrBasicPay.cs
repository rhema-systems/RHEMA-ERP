using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The one answer to "what is this person's monthly basic pay, and where does the figure come from?"
/// </summary>
/// <remarks>
/// <para><b>⚠ There used to be two of these, and they disagreed.</b>
/// <c>PayrollMembershipService.HrBasicPay</c> was made pay-basis aware in lane E1;
/// <c>EmolumentService.GetMonthlyBasicPayAsync</c> kept its own copy that had never heard of
/// <see cref="Employee.PayBasis"/> — so a negotiated employee's benefit enrolment was computed from
/// a notch amount HR had explicitly said was not their pay. Lane E1b (§ 6.5 follow-up) made this
/// the single resolution and pointed both at it, along with the separation settlement's daily rate.</para>
///
/// <para><b>Pure on purpose.</b> It takes what the caller has already loaded rather than doing its
/// own reads, so each caller keeps its own as-of semantics (emoluments resolve a historical date,
/// the reconciliation resolves today) and there is still exactly one rule.</para>
///
/// <para>⚠ <b>The pay basis has no history</b>, and neither does payroll's salary basis (its upsert
/// mutates one row in place — round-2 plan § 7.1 item 3, an ask to the payroll owner). So a
/// historical "were they negotiated in June?" is unanswerable: the CURRENT basis is applied
/// whatever date the caller is asking about, and for a negotiated employee the amount returned is
/// payroll's present figure. The source sentence says so, rather than letting a caller present it
/// as a figure that was true on the date.</para>
/// </remarks>
public static class HrBasicPay
{
    /// <summary>
    /// The figure and its provenance. <c>(null, reason)</c> where there is nothing to quote, so a
    /// caller can print why rather than a misleading zero.
    /// </summary>
    /// <param name="employee">The employee. <see cref="Employee.PayBasis"/> decides which branch applies.</param>
    /// <param name="assignment">
    /// The grade placement in force at the caller's as-of date, or null. ⚠ <b>Must already exclude
    /// withdrawn placements</b> — see <see cref="EmployeeSalaryAssignment.WithdrawnAt"/>. Ignored
    /// entirely when the basis is negotiated.
    /// </param>
    /// <param name="payrollBasic">Payroll's active monthly basic salary for the person, if it has one.</param>
    /// <summary>
    /// The same rule with no payroll figure to hand — for planning reads that sum hundreds of
    /// people and must not call payroll per person (round 2b, R2). A negotiated employee therefore
    /// contributes the flat figure on their record, or nothing; the caller's note says so.
    /// </summary>
    public static (decimal? Amount, string Source) ResolveForPlanning(
        Employee employee, EmployeeSalaryAssignment? assignment)
        => Resolve(employee, assignment, payrollBasic: null);

    public static (decimal? Amount, string Source) Resolve(
        Employee employee, EmployeeSalaryAssignment? assignment, decimal? payrollBasic)
    {
        ArgumentNullException.ThrowIfNull(employee);

        if (!employee.IsOnPayroll)
            return (null, "Not on payroll: no basic pay is on record in HR.");

        if (employee.PayBasis == PayBasis.Negotiated)
        {
            // ⚠ The placement is deliberately NOT consulted here, even if one is in force. A person
            // moved to negotiated pay has their placement withdrawn by the same act, but a stale row
            // — or one that predates this rule — must not resurface as their pay. The negotiated
            // amount is the one entered on the payroll profile; that is what § 6.5.2 settled.
            if (payrollBasic is > 0m)
                return (payrollBasic, "Negotiated — the amount on payroll's salary basis.");
            if (employee.Salary is > 0m)
                return (employee.Salary, "Negotiated — the flat figure on the employee record; payroll has no basis yet.");
            return (null, "Negotiated, but no amount is on record: enter it on the payroll profile.");
        }

        // The level is named only when it is a real tier. A two-tier grade's one implicit level
        // carries the grade's own code (the projection and the HR create both make it so), and
        // "notch 3, level S2 of S2" would be telling the reader about a thing that does not exist.
        var gradeCode = assignment?.Grade?.Code ?? "the placed grade";
        var levelIsReal = assignment?.Level is { } lv && !string.Equals(lv.Code, assignment.Grade?.Code, StringComparison.OrdinalIgnoreCase);

        if (assignment?.Notch?.SalaryAmount is { } notch)
            return (notch, levelIsReal
                ? $"Salary scale — notch {assignment.Notch.NotchNumber}, level {assignment.Level!.Code} of {gradeCode}."
                : $"Salary scale — notch {assignment.Notch.NotchNumber} of {gradeCode}.");
        if (assignment?.Level?.MidSalary is { } mid)
            return (mid, levelIsReal
                ? $"Salary scale — mid-point of level {assignment.Level.Code} of {gradeCode} (no notch chosen)."
                : $"Salary scale — mid-point of grade {gradeCode} (no notch chosen).");
        if (employee.Salary is > 0m)
            return (employee.Salary, "The flat figure on the employee record; not yet placed on the scale.");
        return (null, "On the scale, but not placed on a grade and no flat figure is on record.");
    }
}
