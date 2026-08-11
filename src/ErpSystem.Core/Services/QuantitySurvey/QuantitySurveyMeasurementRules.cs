using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyMeasurementRules
{
    public static (decimal Quantity, string Formula) Calculate(QuantitySurveyMeasurementLineInputDto input)
    {
        if (input.ClientLineKey == Guid.Empty)
            throw new QuantitySurveyMeasurementValidationException("Every measurement row requires a client line identifier.");
        if (input.Timesing <= 0) throw InvalidDimensions();
        decimal raw;
        string formula;
        switch (input.FormulaType)
        {
            case QuantitySurveyMeasurementFormulaType.Count:
                RequireAbsent(input.Length, input.Width, input.Height);
                raw = input.Timesing; formula = "Timesing"; break;
            case QuantitySurveyMeasurementFormulaType.Length:
                RequirePositive(input.Length, "length"); RequireAbsent(input.Width, input.Height);
                raw = input.Timesing * input.Length!.Value; formula = "Timesing × Length"; break;
            case QuantitySurveyMeasurementFormulaType.Area:
                RequirePositive(input.Length, "length"); RequirePositive(input.Width, "width"); RequireAbsent(input.Height);
                raw = input.Timesing * input.Length!.Value * input.Width!.Value; formula = "Timesing × Length × Width"; break;
            case QuantitySurveyMeasurementFormulaType.Volume:
                RequirePositive(input.Length, "length"); RequirePositive(input.Width, "width"); RequirePositive(input.Height, "height");
                raw = input.Timesing * input.Length!.Value * input.Width!.Value * input.Height!.Value;
                formula = "Timesing × Length × Width × Height"; break;
            default: throw new QuantitySurveyMeasurementValidationException("Select a supported measurement formula.");
        }
        var quantity = decimal.Round(input.IsDeduction ? -raw : raw, 4, MidpointRounding.AwayFromZero);
        if (Math.Abs(quantity) > 99999999999999.9999m)
            throw new QuantitySurveyMeasurementValidationException("The calculated measurement exceeds the supported quantity range.");
        return (quantity, input.IsDeduction ? $"-({formula})" : formula);
    }

    public static decimal Total(IEnumerable<QuantitySurveyMeasurementLineInputDto> lines)
    {
        var list = lines.ToList();
        if (list.Count is < 1 or > 100) throw new QuantitySurveyMeasurementValidationException("A sheet requires between 1 and 100 measurement rows.");
        if (list.Select(value => value.ClientLineKey).Distinct().Count() != list.Count)
            throw new QuantitySurveyMeasurementValidationException("Measurement row identifiers must be unique.");
        if (list.Select(value => value.Sequence).Distinct().Count() != list.Count || list.Any(value => value.Sequence < 1))
            throw new QuantitySurveyMeasurementValidationException("Measurement row sequences must be positive and unique.");
        var total = decimal.Round(list.Sum(value => Calculate(value).Quantity), 4, MidpointRounding.AwayFromZero);
        if (total <= 0) throw new QuantitySurveyMeasurementValidationException("The net measured quantity must be greater than zero.");
        return total;
    }

    private static void RequirePositive(decimal? value, string label)
    {
        if (!value.HasValue || value.Value <= 0) throw new QuantitySurveyMeasurementValidationException($"Enter a positive {label} for the selected formula.");
    }
    private static void RequireAbsent(params decimal?[] values)
    {
        if (values.Any(value => value.HasValue)) throw new QuantitySurveyMeasurementValidationException("Remove dimensions that are not used by the selected formula.");
    }
    private static QuantitySurveyMeasurementValidationException InvalidDimensions()
        => new("Timesing and all required dimensions must be greater than zero.");
}
