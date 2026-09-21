using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Which fiscal year a date falls in, from <see cref="CompanyHrPolicySettings.FiscalYearStartMonth"/>.
/// </summary>
/// <remarks>
/// <para>A fiscal year is <b>labelled by the calendar year it starts in</b>: with a July start,
/// 15 March 2028 is in fiscal year 2027 (July 2027 – June 2028). The budget form's period default
/// (<c>fiscalPeriodFor</c> in the frontend) uses the same convention, so a budget labelled 2027
/// and a requisition starting in March 2028 match.</para>
/// <para>Round 2b, R5. Before this the requisition's budget check used the calendar year of the
/// desired start date (`StaffRequisitionService.BuildBudgetCheckAsync`) while the setting sat
/// unread beside it (§ 3 defect 3 of the round-2b plan).</para>
/// </remarks>
public static class HrFiscalYear
{
    public static int For(DateTime date, CompanyHrPolicySettings settings) => For(DateOnly.FromDateTime(date), settings);

    public static int For(DateOnly date, CompanyHrPolicySettings settings)
    {
        var start = settings.FiscalYearStartMonth;
        if (start <= 1 || start > 12) return date.Year;
        return date.Month >= start ? date.Year : date.Year - 1;
    }
}
