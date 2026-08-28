using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyVariationRulesTests
{
    [Theory]
    [InlineData(2.5, 12.345, 30.86)]
    [InlineData(-2, 10, -20)]
    public void CalculateAmount_UsesControlledQuantityAndRate(decimal quantity, decimal rate, decimal expected) =>
        Assert.Equal(expected, QuantitySurveyVariationRules.CalculateAmount(quantity, rate));

    [Fact]
    public void CalculateAmount_RejectsZeroQuantityAndNegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantitySurveyVariationRules.CalculateAmount(0m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantitySurveyVariationRules.CalculateAmount(1m, -1m));
    }

    [Fact]
    public void CalculateRevisedContractSum_AppliesAdditionsAndOmissions()
    {
        Assert.Equal(1_125m, QuantitySurveyVariationRules.CalculateRevisedContractSum(1_000m, 125m));
        Assert.Equal(875m, QuantitySurveyVariationRules.CalculateRevisedContractSum(1_000m, -125m));
    }

    [Fact]
    public void Downstream_application_values_reject_underflow_and_reconcile_final_account_baseline()
    {
        Assert.Equal(1_125m, QuantitySurveyVariationRules.CalculateNonNegativeDownstreamValue(1_000m, 125m, "contract"));
        Assert.Equal(1_000m, QuantitySurveyVariationRules.CalculateFinalAccountContractBaseline(1_125m, 125m));
        Assert.Equal(1_000m, QuantitySurveyVariationRules.CalculateFinalAccountContractBaseline(875m, -125m));
        Assert.Throws<InvalidOperationException>(() => QuantitySurveyVariationRules.CalculateNonNegativeDownstreamValue(100m, -101m, "contract"));
        Assert.Throws<InvalidOperationException>(() => QuantitySurveyVariationRules.CalculateFinalAccountContractBaseline(100m, 101m));
    }

    [Theory]
    [InlineData(QuantitySurveyVariationSourceType.SiteInstruction, true, false, true)]
    [InlineData(QuantitySurveyVariationSourceType.SiteInstruction, false, false, false)]
    [InlineData(QuantitySurveyVariationSourceType.ChangeRequest, false, true, true)]
    [InlineData(QuantitySurveyVariationSourceType.ChangeRequest, true, true, false)]
    [InlineData(QuantitySurveyVariationSourceType.DirectVariation, false, false, true)]
    [InlineData(QuantitySurveyVariationSourceType.ChangeOrder, true, false, false)]
    public void HasValidSourceSelection_EnforcesExclusiveControlledLineage(
        QuantitySurveyVariationSourceType sourceType, bool site, bool change, bool expected) =>
        Assert.Equal(expected, QuantitySurveyVariationRules.HasValidSourceSelection(
            sourceType,
            site ? Guid.NewGuid() : null,
            change ? Guid.NewGuid() : null));

    [Theory]
    [InlineData(ProjectVariationOrderTypes.Daywork, "Daywork")]
    [InlineData(ProjectVariationOrderTypes.AdditionalWork, "Additional Work")]
    [InlineData(ProjectVariationOrderTypes.SiteInstruction, "Site Instruction")]
    [InlineData(ProjectVariationOrderTypes.ChangeOrder, "Change Order")]
    [InlineData(ProjectVariationOrderTypes.ScopeChange, "Variation")]
    public void PolicyRecordType_preserves_all_QS0508_business_families(string value, string expected) =>
        Assert.Equal(expected, QuantitySurveyVariationRules.PolicyRecordType(value));

    [Theory]
    [InlineData(QuantitySurveyVariationSourceType.SiteInstruction, ProjectVariationOrderTypes.SiteInstruction, true)]
    [InlineData(QuantitySurveyVariationSourceType.SiteInstruction, ProjectVariationOrderTypes.Daywork, false)]
    [InlineData(QuantitySurveyVariationSourceType.ChangeOrder, ProjectVariationOrderTypes.ChangeOrder, true)]
    [InlineData(QuantitySurveyVariationSourceType.DirectVariation, ProjectVariationOrderTypes.Daywork, true)]
    public void Record_type_and_governed_source_must_be_compatible(
        QuantitySurveyVariationSourceType sourceType, string recordType, bool expected) =>
        Assert.Equal(expected, QuantitySurveyVariationRules.IsSourceCompatibleWithRecordType(sourceType, recordType));
}
