using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyValuationWorksheetRulesTests
{
    [Fact]
    public void Calculate_reconciles_cumulative_and_current_period_values_per_boq_line()
    {
        var result = QuantitySurveyValuationWorksheetRules.Calculate(Source(), 9m, 8m, 10m, "One unit held for review.");

        result.MeasuredToDateValue.Should().Be(1_000m);
        result.CurrentClaimedValue.Should().Be(900m);
        result.CurrentCertifiedValue.Should().Be(800m);
        result.CurrentPeriodCertifiedValue.Should().Be(300m);
        result.DisputedQuantity.Should().Be(1m);
        result.DisputedValue.Should().Be(100m);
        result.RetentionToDateValue.Should().Be(80m);
        result.CurrentRetentionValue.Should().Be(30m);
        result.NetCurrentValue.Should().Be(270m);
    }

    [Theory]
    [InlineData(11, 10, "Claim exceeds measurement")]
    [InlineData(8, 9, "Certification exceeds claim")]
    [InlineData(5, 4, "Certification regresses")]
    public void Calculate_rejects_impossible_quantity_lifecycles(double claimed, double certified, string _)
    {
        Action action = () => QuantitySurveyValuationWorksheetRules.Calculate(
            Source(), (decimal)claimed, (decimal)certified, 10m, "Review note");
        action.Should().Throw<QuantitySurveyValuationWorksheetValidationException>();
    }

    [Fact]
    public void Calculate_requires_a_note_for_every_disputed_quantity()
    {
        Action action = () => QuantitySurveyValuationWorksheetRules.Calculate(Source(), 9m, 8m, 10m, null);
        action.Should().Throw<QuantitySurveyValuationWorksheetValidationException>()
            .WithMessage("*review note*");
    }

    [Fact]
    public void Calculate_allows_an_unreviewed_contractor_claim_before_qs_certification()
    {
        var result = QuantitySurveyValuationWorksheetRules.Calculate(
            Source(), 9m, 5m, 10m, null, requireDisputeReviewNote: false);

        result.CurrentClaimedQuantity.Should().Be(9m);
        result.CurrentCertifiedQuantity.Should().Be(5m);
        result.DisputedQuantity.Should().Be(4m);
        result.ReviewNote.Should().BeNull();
    }

    [Fact]
    public void Total_equals_the_sum_of_governed_line_calculations()
    {
        var first = QuantitySurveyValuationWorksheetRules.Calculate(Source(), 9m, 9m, 10m, null);
        var secondSource = Source() with
        {
            ProjectBoqVersionLineId = Guid.NewGuid(), BoqLineKey = Guid.NewGuid(), Sequence = 2,
            MeasuredToDateQuantity = 5m, PreviouslyCertifiedQuantity = 0m,
            PreviouslyCertifiedValue = 0m, PreviousRetentionValue = 0m
        };
        var second = QuantitySurveyValuationWorksheetRules.Calculate(secondSource, 5m, 4m, 10m, "One unit disputed.");

        var total = QuantitySurveyValuationWorksheetRules.Total([first, second]);
        total.CurrentPeriodCertifiedValue.Should().Be(first.CurrentPeriodCertifiedValue + second.CurrentPeriodCertifiedValue);
        total.CurrentRetentionValue.Should().Be(first.CurrentRetentionValue + second.CurrentRetentionValue);
        total.NetCurrentValue.Should().Be(first.NetCurrentValue + second.NetCurrentValue);
    }

    [Theory]
    [InlineData("Draft", "ContractorSubmitted")]
    [InlineData("Draft", "QsVetted")]
    [InlineData("ContractorSubmitted", "UnderQsReview")]
    [InlineData("UnderQsReview", "QsVetted")]
    [InlineData("QsVetted", "ConsultantEndorsed")]
    [InlineData("ConsultantEndorsed", "PendingApproval")]
    [InlineData("PendingApproval", "Approved")]
    [InlineData("PendingApproval", "Rejected")]
    public void RequireTransition_accepts_only_the_governed_forward_lifecycle(string current, string next)
    {
        Action action = () => QuantitySurveyValuationWorksheetRules.RequireTransition(current, next);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("Draft", "Approved")]
    [InlineData("QsVetted", "Approved")]
    [InlineData("Approved", "PendingApproval")]
    [InlineData("Rejected", "Draft")]
    public void RequireTransition_blocks_bypass_and_terminal_reopening(string current, string next)
    {
        Action action = () => QuantitySurveyValuationWorksheetRules.RequireTransition(current, next);
        action.Should().Throw<QuantitySurveyValuationWorksheetValidationException>();
    }

    [Fact]
    public void RequireIndependentApprover_blocks_every_prior_lifecycle_actor()
    {
        var actor = Guid.NewGuid();
        Action action = () => QuantitySurveyValuationWorksheetRules.RequireIndependentApprover(
            actor, Guid.NewGuid(), Guid.NewGuid(), actor, Guid.NewGuid());
        action.Should().Throw<QuantitySurveyValuationWorksheetValidationException>()
            .WithMessage("*Maker-checker*");
    }

    [Fact]
    public void Certificate_readiness_requires_workflow_approval_and_all_configured_controls()
    {
        var actor = Guid.NewGuid();
        QuantitySurveyValuationWorksheetRules.IsCertificateReady(
                "Approved", "Approved", true, actor, true, Guid.NewGuid(), true, 1,
                Guid.NewGuid(), Guid.NewGuid())
            .Should().BeTrue();
        QuantitySurveyValuationWorksheetRules.IsCertificateReady(
                "Approved", "Approved", true, actor, true, null, true, 1,
                Guid.NewGuid(), Guid.NewGuid())
            .Should().BeFalse();
    }

    private static QuantitySurveyValuationSourceLine Source() => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, "1.01", "Concrete work", "m3", "GHS",
        12m, 100m, 10m, 5m, 500m, 50m);
}
