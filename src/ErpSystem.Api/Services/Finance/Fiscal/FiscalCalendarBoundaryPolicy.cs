using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Services.Finance.Fiscal;

internal static class FiscalCalendarBoundaryPolicy
{
    internal static bool IsQuarterEnd(PeriodType periodType, int periodNumber, int fiscalYearPeriodCount)
    {
        return periodType switch
        {
            PeriodType.Quarterly => true,
            PeriodType.Monthly => periodNumber % 3 == 0,
            PeriodType.Weekly when fiscalYearPeriodCount is 52 or 53 =>
                periodNumber is 13 or 26 or 39 || periodNumber == fiscalYearPeriodCount,
            _ => false
        };
    }
}
