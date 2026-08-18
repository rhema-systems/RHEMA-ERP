using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveySubcontractChargeRulesTests
{
    [Fact]
    public void Draft_validation_enforces_controlled_type_amount_period_and_policy()
    {
        var date = new DateTime(2026, 8, 11);
        var issues = QuantitySurveySubcontractChargeRules.ValidateDraft(
            "Other", 1_001m, date, date.AddDays(91), 1_000m,
            controlBackCharges: false, controlContraCharges: false);

        issues.Should().Contain(value => value.Contains("Back charge or Contra charge"))
            .And.Contain(value => value.Contains("cannot exceed the subcontract value"))
            .And.Contain(value => value.Contains("cannot exceed 90 days"));
    }

    [Fact]
    public void Disabled_charge_type_is_rejected_by_the_effective_policy()
    {
        var date = new DateTime(2026, 8, 11);
        QuantitySurveySubcontractChargeRules.ValidateDraft(
                QuantitySurveySubcontractChargeTypes.BackCharge, 100m, date, date.AddDays(7),
                1_000m, controlBackCharges: false, controlContraCharges: true)
            .Should().ContainSingle(value => value.Contains("disabled by the effective QS policy"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Approved_amount_must_be_positive_and_within_the_notified_amount(decimal approved) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            QuantitySurveySubcontractChargeRules.ValidateApprovedAmount(100m, approved));

    [Fact]
    public void Approved_charge_totals_are_server_calculated_by_controlled_type()
    {
        var result = QuantitySurveySubcontractChargeRules.SumApproved([
            (QuantitySurveySubcontractChargeTypes.BackCharge, 12.345m),
            (QuantitySurveySubcontractChargeTypes.BackCharge, 2.335m),
            (QuantitySurveySubcontractChargeTypes.ContraCharge, 5.005m)
        ]);

        result.Should().Be(new QuantitySurveySubcontractChargeTotals(14.69m, 5.01m, 19.70m));
    }
}
