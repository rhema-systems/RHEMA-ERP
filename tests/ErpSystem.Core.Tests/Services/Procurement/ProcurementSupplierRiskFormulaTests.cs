using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierRiskFormulaTests
{
    [Fact]
    public void Weighted_score_uses_exact_policy_weights()
    {
        var result = ProcurementSupplierRiskFormula.CalculateWeightedScore(
        [
            new(90m, 25m),
            new(80m, 25m),
            new(70m, 30m),
            new(40m, 20m)
        ]);

        result.Should().Be(71.50m);
    }

    [Fact]
    public void Spend_share_is_currency_bucket_safe_and_deterministic()
    {
        ProcurementSupplierRiskFormula.CalculateSpendShare(250m, 1000m)
            .Should().Be(25m);
        ProcurementSupplierRiskFormula.CalculateSpendShare(0m, 0m)
            .Should().Be(0m);
        var action = () =>
            ProcurementSupplierRiskFormula.CalculateSpendShare(1001m, 1000m);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dec011_requires_weighted_dimensions_contiguous_bands_and_window()
    {
        var value = new ProcurementSupplierRiskDecisionValueDto
        {
            EffectiveFrom = DateTime.UtcNow.Date,
            ReviewFrequencyMonths = 12,
            ExposureWindowMonths = 12,
            RiskDimensions = ["Delivery=40", "Quality=30", "Compliance=30"],
            RiskBands = ["High=0-50", "Medium=50-75", "Low=75-100"],
            ConcentrationLimitPercent = 35,
            MinimumScore = 70,
            EligibilityAction = ProcurementSupplierRiskEligibilityAction.EscalationRequired,
            PerformanceWindowMonths = 12,
            PerformanceDimensions =
            [
                "DeliveryTimeliness=15",
                "GrnQuality=15",
                "RejectionRate=15",
                "PriceCompetitiveness=15",
                "Responsiveness=10",
                "ComplaintResolution=10",
                "ContractCompletion=20"
            ],
            PerformanceBands =
                ["Unsatisfactory=0-50", "ImprovementRequired=50-75", "Satisfactory=75-100"],
            MinimumPerformanceDataCoveragePercent = 60,
            ResponseTargetHours = 48,
            PerformanceEligibilityAction =
                ProcurementSupplierRiskEligibilityAction.AwardHardStop
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(value, new ValidationContext(value), results, true)
            .Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void Dec011_rejects_ambiguous_band_and_weight_configuration()
    {
        var value = new ProcurementSupplierRiskDecisionValueDto
        {
            EffectiveFrom = DateTime.UtcNow.Date,
            ReviewFrequencyMonths = 12,
            ExposureWindowMonths = 12,
            RiskDimensions = ["Delivery=60", "Quality=30"],
            RiskBands = ["High=0-50", "Low=60-100"],
            ConcentrationLimitPercent = 35,
            MinimumScore = 70,
            EligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop,
            PerformanceWindowMonths = 12,
            PerformanceDimensions =
            [
                "DeliveryTimeliness=15",
                "GrnQuality=15",
                "RejectionRate=15",
                "PriceCompetitiveness=15",
                "Responsiveness=10",
                "ComplaintResolution=10",
                "ContractCompletion=20"
            ],
            PerformanceBands =
                ["Unsatisfactory=0-50", "ImprovementRequired=50-75", "Satisfactory=75-100"],
            MinimumPerformanceDataCoveragePercent = 60,
            ResponseTargetHours = 48,
            PerformanceEligibilityAction =
                ProcurementSupplierRiskEligibilityAction.AwardHardStop
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(value, new ValidationContext(value), results, true)
            .Should().BeFalse();
        results.Should().Contain(item =>
            item.MemberNames.Contains(nameof(value.RiskDimensions)));
        results.Should().Contain(item =>
            item.MemberNames.Contains(nameof(value.RiskBands)));
    }
}
