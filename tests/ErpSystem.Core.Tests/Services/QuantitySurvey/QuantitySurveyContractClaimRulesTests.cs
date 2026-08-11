using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyContractClaimRulesTests
{
    [Theory]
    [InlineData(QuantitySurveyContractClaimType.ExtensionOfTime, false, true, true)]
    [InlineData(QuantitySurveyContractClaimType.ExtensionOfTime, true, true, false)]
    [InlineData(QuantitySurveyContractClaimType.Variation, true, false, true)]
    [InlineData(QuantitySurveyContractClaimType.Daywork, true, false, true)]
    [InlineData(QuantitySurveyContractClaimType.AdditionalWork, false, false, false)]
    [InlineData(QuantitySurveyContractClaimType.LossAndExpense, false, false, true)]
    [InlineData(QuantitySurveyContractClaimType.Other, true, false, false)]
    public void HasValidSource_enforces_the_controlled_claim_lineage(
        QuantitySurveyContractClaimType type, bool variation, bool extension, bool expected) =>
        Assert.Equal(expected, QuantitySurveyContractClaimRules.HasValidSource(type,
            variation ? Guid.NewGuid() : null, extension ? Guid.NewGuid() : null));

    [Theory]
    [InlineData(100, 75, 25)]
    [InlineData(100, 0, 100)]
    [InlineData(100, 100, 0)]
    public void RejectedAmount_is_server_derived(decimal claimed, decimal accepted, decimal expected) =>
        Assert.Equal(expected, QuantitySurveyContractClaimRules.RejectedAmount(claimed, accepted));

    [Fact]
    public void RejectedAmount_rejects_an_assessment_above_the_claim() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantitySurveyContractClaimRules.RejectedAmount(100m, 101m));

    [Theory]
    [InlineData(100, 0, QuantitySurveyClaimSettlementStatus.Pending)]
    [InlineData(100, 25, QuantitySurveyClaimSettlementStatus.PartiallySettled)]
    [InlineData(100, 100, QuantitySurveyClaimSettlementStatus.Settled)]
    public void SettlementStatus_tracks_the_governed_cumulative_total(
        decimal approved, decimal settled, QuantitySurveyClaimSettlementStatus expected) =>
        Assert.Equal(expected, QuantitySurveyContractClaimRules.SettlementStatus(approved, settled));
}
