using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyDesignRevisionImpactRules
{
    public static void ValidateLine(QuantitySurveyDesignImpactRoute route, QuantitySurveyDesignImpactType type,
        decimal previousQuantity, decimal? indicativeQuantity)
    {
        if (previousQuantity < 0) throw new ArgumentOutOfRangeException(nameof(previousQuantity));
        if (route == QuantitySurveyDesignImpactRoute.Measurement &&
            type is QuantitySurveyDesignImpactType.ScopeAddition or QuantitySurveyDesignImpactType.Omission or QuantitySurveyDesignImpactType.RateReviewOnly)
            throw new ArgumentException("Measurement routing accepts only remeasurement or quantity-change impacts.");
        if (route == QuantitySurveyDesignImpactRoute.Variation && type == QuantitySurveyDesignImpactType.RemeasurementRequired)
            throw new ArgumentException("Remeasurement-required impacts must use the measurement workflow route.");
        if (indicativeQuantity < 0) throw new ArgumentOutOfRangeException(nameof(indicativeQuantity));
        if (type == QuantitySurveyDesignImpactType.QuantityIncrease && (!indicativeQuantity.HasValue || indicativeQuantity <= previousQuantity))
            throw new ArgumentException("A quantity increase requires an indicative quantity above the approved quantity.");
        if (type == QuantitySurveyDesignImpactType.QuantityDecrease && (!indicativeQuantity.HasValue || indicativeQuantity >= previousQuantity))
            throw new ArgumentException("A quantity decrease requires an indicative quantity below the approved quantity.");
        if (type == QuantitySurveyDesignImpactType.Omission && indicativeQuantity is not (null or 0))
            throw new ArgumentException("An omission can use only a zero indicative quantity.");
    }
}
