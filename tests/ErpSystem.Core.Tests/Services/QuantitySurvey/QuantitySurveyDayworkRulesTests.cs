using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyDayworkRulesTests
{
    [Theory]
    [InlineData(ProjectVariationOrderTypes.Daywork, true)]
    [InlineData(ProjectVariationOrderTypes.AdditionalWork, true)]
    [InlineData(ProjectVariationOrderTypes.ChangeOrder, false)]
    public void Supported_parent_types_are_explicit(string type, bool expected) =>
        Assert.Equal(expected, QuantitySurveyDayworkRules.IsSupportedVariationType(type));

    [Theory]
    [InlineData(QuantitySurveyRateItemCategory.Labour, QuantitySurveyDayworkLineType.Labour)]
    [InlineData(QuantitySurveyRateItemCategory.Material, QuantitySurveyDayworkLineType.Material)]
    [InlineData(QuantitySurveyRateItemCategory.Plant, QuantitySurveyDayworkLineType.Plant)]
    [InlineData(QuantitySurveyRateItemCategory.Equipment, QuantitySurveyDayworkLineType.Plant)]
    public void Published_rate_categories_map_to_controlled_daywork_families(
        QuantitySurveyRateItemCategory category, QuantitySurveyDayworkLineType expected) =>
        Assert.Equal(expected, QuantitySurveyDayworkRules.MapRateCategory(category));

    [Fact]
    public void Line_amount_is_rounded_and_rejects_invalid_inputs()
    {
        Assert.Equal(30.86m, QuantitySurveyDayworkRules.LineAmount(2.5m, 12.345m));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantitySurveyDayworkRules.LineAmount(0m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantitySurveyDayworkRules.LineAmount(1m, -1m));
    }

    [Theory]
    [InlineData(QuantitySurveyDayworkSheetStatus.Draft, true)]
    [InlineData(QuantitySurveyDayworkSheetStatus.Rejected, true)]
    [InlineData(QuantitySurveyDayworkSheetStatus.ContractorSigned, false)]
    [InlineData(QuantitySurveyDayworkSheetStatus.Verified, false)]
    public void Only_draft_or_rejected_sheets_are_editable(QuantitySurveyDayworkSheetStatus status, bool expected) =>
        Assert.Equal(expected, QuantitySurveyDayworkRules.CanEdit(status));
}
