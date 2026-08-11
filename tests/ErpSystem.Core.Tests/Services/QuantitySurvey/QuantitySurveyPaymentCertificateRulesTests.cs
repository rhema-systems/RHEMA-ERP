using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyPaymentCertificateRulesTests
{
    [Fact]
    public void Calculate_reconciles_cumulative_previous_deductions_tax_and_net_payable()
    {
        var value = QuantitySurveyPaymentCertificateRules.Calculate(
            1_500m, 1_000m, 50m, 10m, 25m, 80m, 20m, 15m, 5m, 64.25m);
        value.GrossCurrent.Should().Be(600m);
        value.NetCurrent.Should().Be(579.25m);
    }

    [Fact]
    public void Calculate_retains_inclusive_tax_as_evidence_without_adding_it_twice()
    {
        var value = QuantitySurveyPaymentCertificateRules.Calculate(
            1_500m, 1_000m, 50m, 10m, 25m, 0m, 0m, 15m, 5m, 52.50m, addTaxToPayable: false);
        value.Tax.Should().Be(52.50m);
        value.NetCurrent.Should().Be(415m);
    }

    [Theory]
    [InlineData(100, 101, 0, 0, 0, 0, 0, 0)]
    [InlineData(100, 0, 80, 0, 20, 1, 0, 0)]
    public void Calculate_rejects_regression_or_deductions_above_the_payable(
        decimal certified, decimal previous, decimal retention, decimal release,
        decimal advance, decimal material, decimal other, decimal tax)
    {
        Action action = () => QuantitySurveyPaymentCertificateRules.Calculate(
            certified, previous, retention, release, advance, 0m, 0m, material, other, tax);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Independent_approval_blocks_the_preparer_and_submitter()
    {
        var preparer = Guid.NewGuid();
        var submitter = Guid.NewGuid();
        Action prepared = () => QuantitySurveyPaymentCertificateRules.RequireIndependentApprover(preparer, submitter, preparer);
        Action submitted = () => QuantitySurveyPaymentCertificateRules.RequireIndependentApprover(preparer, submitter, submitter);
        prepared.Should().Throw<InvalidOperationException>();
        submitted.Should().Throw<InvalidOperationException>();
        QuantitySurveyPaymentCertificateRules.RequireIndependentApprover(preparer, submitter, Guid.NewGuid());
    }
}
