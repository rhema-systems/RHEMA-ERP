using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyDayworkRules
{
    public static bool IsSupportedVariationType(string? type) =>
        type is ProjectVariationOrderTypes.Daywork or ProjectVariationOrderTypes.AdditionalWork;

    public static QuantitySurveyDayworkLineType? MapRateCategory(QuantitySurveyRateItemCategory category) => category switch
    {
        QuantitySurveyRateItemCategory.Labour => QuantitySurveyDayworkLineType.Labour,
        QuantitySurveyRateItemCategory.Material => QuantitySurveyDayworkLineType.Material,
        QuantitySurveyRateItemCategory.Plant or QuantitySurveyRateItemCategory.Equipment => QuantitySurveyDayworkLineType.Plant,
        _ => null
    };

    public static decimal LineAmount(decimal quantity, decimal unitRate)
    {
        if (quantity <= 0 || unitRate < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        return decimal.Round(quantity * unitRate, 2, MidpointRounding.AwayFromZero);
    }

    public static bool CanEdit(QuantitySurveyDayworkSheetStatus status) =>
        status is QuantitySurveyDayworkSheetStatus.Draft or QuantitySurveyDayworkSheetStatus.Rejected;
}
