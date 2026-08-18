using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyMaterialReconciliationRules
{
    public static decimal AppliedRate(
        QuantitySurveyMaterialValuationBasis basis,
        decimal? deliveredUnitCost,
        decimal? approvedUnitRate)
    {
        if (deliveredUnitCost < 0m || approvedUnitRate < 0m)
            throw new InvalidOperationException("Material rates cannot be negative.");
        var value = basis switch
        {
            QuantitySurveyMaterialValuationBasis.DeliveredCost when deliveredUnitCost > 0m => deliveredUnitCost.Value,
            QuantitySurveyMaterialValuationBasis.ApprovedRate when approvedUnitRate > 0m => approvedUnitRate.Value,
            QuantitySurveyMaterialValuationBasis.LowerOfCostOrApprovedRate when deliveredUnitCost > 0m && approvedUnitRate > 0m
                => Math.Min(deliveredUnitCost.Value, approvedUnitRate.Value),
            _ => throw new InvalidOperationException("The selected material does not contain the rates required by the effective valuation basis.")
        };
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    public static decimal LineValue(decimal quantity, decimal rate)
    {
        if (quantity <= 0m || rate <= 0m)
            throw new InvalidOperationException("Material quantity and applied rate must be greater than zero.");
        return decimal.Round(quantity * rate, 2, MidpointRounding.AwayFromZero);
    }
}
