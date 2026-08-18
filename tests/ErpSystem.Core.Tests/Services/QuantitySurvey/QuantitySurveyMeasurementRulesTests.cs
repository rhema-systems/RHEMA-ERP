using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyMeasurementRulesTests
{
    [Theory]
    [InlineData(QuantitySurveyMeasurementFormulaType.Count, 3d, null, null, null, 3d)]
    [InlineData(QuantitySurveyMeasurementFormulaType.Length, 2d, 4d, null, null, 8d)]
    [InlineData(QuantitySurveyMeasurementFormulaType.Area, 2d, 4d, 3d, null, 24d)]
    [InlineData(QuantitySurveyMeasurementFormulaType.Volume, 2d, 4d, 3d, 5d, 120d)]
    public void Calculate_uses_only_the_typed_server_formula(
        QuantitySurveyMeasurementFormulaType formula, double timesing, double? length,
        double? width, double? height, double expected)
    {
        var result = QuantitySurveyMeasurementRules.Calculate(Line(formula, (decimal)timesing,
            length.HasValue ? (decimal)length.Value : null, width.HasValue ? (decimal)width.Value : null,
            height.HasValue ? (decimal)height.Value : null));
        result.Quantity.Should().Be((decimal)expected);
        result.Formula.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Calculate_applies_deductions_as_negative_quantities()
    {
        var line = Line(QuantitySurveyMeasurementFormulaType.Area, 2, 4, 3, null) with { IsDeduction = true };
        QuantitySurveyMeasurementRules.Calculate(line).Quantity.Should().Be(-24);
    }

    [Fact]
    public void Calculate_rejects_missing_or_unused_dimensions()
    {
        Action missing = () => QuantitySurveyMeasurementRules.Calculate(Line(QuantitySurveyMeasurementFormulaType.Volume, 1, 2, 3, null));
        Action unused = () => QuantitySurveyMeasurementRules.Calculate(Line(QuantitySurveyMeasurementFormulaType.Count, 1, 2, null, null));
        missing.Should().Throw<QuantitySurveyMeasurementValidationException>();
        unused.Should().Throw<QuantitySurveyMeasurementValidationException>();
    }

    [Fact]
    public void Total_rejects_duplicate_keys_sequences_and_non_positive_net_values()
    {
        var id = Guid.NewGuid();
        var first = Line(QuantitySurveyMeasurementFormulaType.Count, 1, null, null, null) with { ClientLineKey = id, Sequence = 1 };
        var duplicate = first with { Description = "Duplicate" };
        Action duplicateKeys = () => QuantitySurveyMeasurementRules.Total([first, duplicate]);
        Action zeroNet = () => QuantitySurveyMeasurementRules.Total([first, first with { ClientLineKey = Guid.NewGuid(), Sequence = 2, IsDeduction = true }]);
        duplicateKeys.Should().Throw<QuantitySurveyMeasurementValidationException>();
        zeroNet.Should().Throw<QuantitySurveyMeasurementValidationException>();
    }

    private static QuantitySurveyMeasurementLineInputDto Line(QuantitySurveyMeasurementFormulaType formula, decimal timesing,
        decimal? length, decimal? width, decimal? height) => new()
    {
        ClientLineKey = Guid.NewGuid(), Sequence = 1, Description = "Measured work",
        FormulaType = formula, Timesing = timesing, Length = length, Width = width, Height = height
    };

}
