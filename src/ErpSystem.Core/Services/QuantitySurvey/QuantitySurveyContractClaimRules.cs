using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyContractClaimRules
{
    public static bool HasValidSource(QuantitySurveyContractClaimType type, Guid? variationId, Guid? extensionId)
        => type switch
        {
            QuantitySurveyContractClaimType.ExtensionOfTime => extensionId.HasValue && !variationId.HasValue,
            QuantitySurveyContractClaimType.Variation or QuantitySurveyContractClaimType.Daywork or QuantitySurveyContractClaimType.AdditionalWork
                => variationId.HasValue && !extensionId.HasValue,
            _ => !variationId.HasValue && !extensionId.HasValue
        };

    public static decimal RejectedAmount(decimal claimed, decimal accepted)
    {
        if (claimed <= 0 || accepted < 0 || accepted > claimed)
            throw new ArgumentOutOfRangeException(nameof(accepted), "The assessed amount must be between zero and the claimed amount.");
        return decimal.Round(claimed - accepted, 2, MidpointRounding.AwayFromZero);
    }

    public static QuantitySurveyClaimSettlementStatus SettlementStatus(decimal approved, decimal settled)
    {
        if (approved <= 0 || settled < 0 || settled > approved)
            throw new ArgumentOutOfRangeException(nameof(settled), "The settled amount must be between zero and the approved amount.");
        if (settled == 0) return QuantitySurveyClaimSettlementStatus.Pending;
        return settled == approved ? QuantitySurveyClaimSettlementStatus.Settled : QuantitySurveyClaimSettlementStatus.PartiallySettled;
    }

    public static bool CanEdit(string status) => status is QuantitySurveyContractClaimStatuses.Draft or QuantitySurveyContractClaimStatuses.Rejected;
}
