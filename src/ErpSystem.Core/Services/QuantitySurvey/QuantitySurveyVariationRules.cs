using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyVariationRules
{
    public static decimal CalculateAmount(decimal quantityChange, decimal unitRate)
    {
        if (quantityChange == 0m)
            throw new ArgumentOutOfRangeException(nameof(quantityChange), "Variation quantity changes cannot be zero.");
        if (unitRate < 0m)
            throw new ArgumentOutOfRangeException(nameof(unitRate), "Variation rates cannot be negative.");

        return decimal.Round(quantityChange * unitRate, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateRevisedContractSum(decimal originalContractSum, decimal approvedVariationAmount) =>
        decimal.Round(originalContractSum + approvedVariationAmount, 2, MidpointRounding.AwayFromZero);

    public static decimal CalculateNonNegativeDownstreamValue(decimal currentValue, decimal approvedVariationAmount, string valueName)
    {
        if (currentValue < 0m) throw new ArgumentOutOfRangeException(nameof(currentValue), $"{valueName} cannot be negative.");
        var result = CalculateRevisedContractSum(currentValue, approvedVariationAmount);
        return result < 0m
            ? throw new InvalidOperationException($"The approved variation would reduce {valueName} below zero.")
            : result;
    }

    public static decimal CalculateFinalAccountContractBaseline(decimal currentContractValue, decimal contractVariationAmount)
    {
        if (currentContractValue < 0m)
            throw new ArgumentOutOfRangeException(nameof(currentContractValue), "Final-account contract values cannot be negative.");
        var result = decimal.Round(currentContractValue - contractVariationAmount, 2, MidpointRounding.AwayFromZero);
        return result < 0m
            ? throw new InvalidOperationException("Applied variation lineage exceeds the current Works contract value.")
            : result;
    }

    public static bool HasValidSourceSelection(
        QuantitySurveyVariationSourceType sourceType,
        Guid? siteInstructionId,
        Guid? changeRequestId) => sourceType switch
        {
            QuantitySurveyVariationSourceType.SiteInstruction => siteInstructionId.HasValue && !changeRequestId.HasValue,
            QuantitySurveyVariationSourceType.ChangeRequest => changeRequestId.HasValue && !siteInstructionId.HasValue,
            QuantitySurveyVariationSourceType.DirectVariation or QuantitySurveyVariationSourceType.ChangeOrder =>
                !siteInstructionId.HasValue && !changeRequestId.HasValue,
            _ => false
        };

    public static bool IsSourceCompatibleWithRecordType(
        QuantitySurveyVariationSourceType sourceType,
        string recordType) => sourceType switch
        {
            QuantitySurveyVariationSourceType.SiteInstruction => recordType == ProjectVariationOrderTypes.SiteInstruction,
            QuantitySurveyVariationSourceType.ChangeOrder => recordType == ProjectVariationOrderTypes.ChangeOrder,
            _ => recordType is not (ProjectVariationOrderTypes.SiteInstruction or ProjectVariationOrderTypes.ChangeOrder)
        };

    public static string PolicyRecordType(string recordType) => recordType switch
    {
        ProjectVariationOrderTypes.Daywork => "Daywork",
        ProjectVariationOrderTypes.AdditionalWork => "Additional Work",
        ProjectVariationOrderTypes.SiteInstruction => "Site Instruction",
        ProjectVariationOrderTypes.ChangeOrder => "Change Order",
        _ => "Variation"
    };
}
