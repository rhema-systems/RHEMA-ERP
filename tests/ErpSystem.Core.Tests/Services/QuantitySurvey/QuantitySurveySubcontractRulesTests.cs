using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveySubcontractRulesTests
{
    [Fact]
    public void Contract_validation_requires_controlled_parent_partner_term_value_and_dates()
    {
        var issues = QuantitySurveySubcontractRules.ValidateContract(
            100m, 120m, 101m, new DateTime(2026, 8, 10), new DateTime(2026, 8, 9),
            parentAllowsSubcontracting: false, policyControlsSubcontracts: false,
            activePartner: false, paymentTermValid: false);

        issues.Should().HaveCount(7)
            .And.Contain(value => value.Contains("parent Works contract"))
            .And.Contain(value => value.Contains("effective QS policy"))
            .And.Contain(value => value.Contains("Finance payment term"))
            .And.Contain(value => value.Contains("cannot exceed"))
            .And.Contain(value => value.Contains("End date"));
    }

    [Fact]
    public void Valuation_amounts_are_server_calculated_and_rounded()
    {
        var result = QuantitySurveySubcontractRules.Calculate(
            subcontractValue: 1_000m, claimedToDate: 600m, assessedToDate: 500m,
            previouslyCertified: 200m, retentionPercent: 10m, retentionReleased: 10m,
            approvedBackCharge: 0m, approvedContraCharge: 0m, tax: 45m);

        result.Should().Be(new QuantitySurveySubcontractValuationAmounts(
            PreviouslyCertified: 200m, CurrentCertified: 300m, RetentionHeld: 30m,
            RetentionReleased: 10m, BackCharge: 0m, ContraCharge: 0m, Tax: 45m, Net: 325m));
    }

    [Fact]
    public void Inclusive_tax_is_disclosed_but_not_added_to_the_payable_total_twice()
    {
        var result = QuantitySurveySubcontractRules.Calculate(
            1_000m, 600m, 500m, 200m, 10m, 10m, 0m, 0m, 45m,
            taxInclusive: true);

        result.Tax.Should().Be(45m);
        result.Net.Should().Be(280m);
    }

    [Theory]
    [InlineData(1_001, 500, 100)]
    [InlineData(500, 499, 500)]
    public void Valuation_rejects_claim_or_assessment_outside_frozen_boundaries(
        decimal claimed, decimal assessed, decimal previous) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => QuantitySurveySubcontractRules.Calculate(
            1_000m, claimed, assessed, previous, 5m, 0m, 0m, 0m, 0m));
}
