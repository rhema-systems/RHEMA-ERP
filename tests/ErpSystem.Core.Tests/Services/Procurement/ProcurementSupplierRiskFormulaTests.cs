using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.Configuration;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierRiskFormulaTests
{
    [Theory]
    [InlineData("SupplierPerformance", ProcurementSupplierRiskDimension.SupplierPerformance)]
    [InlineData("Performance", ProcurementSupplierRiskDimension.SupplierPerformance)]
    [InlineData("OverallPerformance", ProcurementSupplierRiskDimension.SupplierPerformance)]
    [InlineData("Delivery", ProcurementSupplierRiskDimension.Delivery)]
    [InlineData("Quality", ProcurementSupplierRiskDimension.Quality)]
    [InlineData("CostCompetitiveness", ProcurementSupplierRiskDimension.CostCompetitiveness)]
    [InlineData("Compliance", ProcurementSupplierRiskDimension.Compliance)]
    [InlineData("DueDiligence", ProcurementSupplierRiskDimension.DueDiligence)]
    [InlineData("FinancialStability", ProcurementSupplierRiskDimension.FinancialStability)]
    [InlineData("SpendDiversification", ProcurementSupplierRiskDimension.SpendDiversification)]
    [InlineData("SingleSourceDependency", ProcurementSupplierRiskDimension.SingleSourceDependency)]
    public void Dec011_and_runtime_share_supported_names_aliases_and_case_behavior(
        string name, ProcurementSupplierRiskDimension expected)
    {
        foreach (var variant in new[] { name, name.ToLowerInvariant(), name.ToUpperInvariant(), $" {name} " })
        {
            ProcurementSupplierRiskDimensionCatalog.TryResolve(variant, out var dimension)
                .Should().BeTrue();
            dimension.Should().Be(expected);
            var value = ValidRiskPolicy($"{variant}=100");
            var validation = new List<ValidationResult>();
            Validator.TryValidateObject(value, new ValidationContext(value), validation, true)
                .Should().BeTrue();
            validation.Should().BeEmpty();
        }
    }

    [Fact]
    public void Supported_catalog_contains_only_the_existing_engine_contract()
    {
        ProcurementSupplierRiskDimensionCatalog.SupportedNames.Should().BeEquivalentTo(new[]
        {
            "SupplierPerformance", "Performance", "OverallPerformance", "Delivery", "Quality",
            "CostCompetitiveness", "Compliance", "DueDiligence", "FinancialStability",
            "SpendDiversification", "SingleSourceDependency"
        });
        var resolved = ProcurementSupplierRiskDimensionCatalog.SupportedNames.Select(name =>
        {
            ProcurementSupplierRiskDimensionCatalog.TryResolve(name, out var dimension);
            return dimension;
        }).Distinct();
        resolved.Should().BeEquivalentTo(Enum.GetValues<ProcurementSupplierRiskDimension>());
    }

    [Theory]
    [InlineData("Financial")]
    [InlineData("financial")]
    [InlineData("Financial Stability")]
    [InlineData("Financial-Stability")]
    [InlineData("Reputation")]
    [InlineData("DeliveryTimeliness")]
    [InlineData("0")]
    [InlineData("999")]
    public void Unsupported_metric_is_rejected_by_dto_and_governed_registry_without_silent_alias(
        string name)
    {
        ProcurementSupplierRiskDimensionCatalog.TryResolve(name, out _).Should().BeFalse();
        var value = ValidRiskPolicy($"{name}=50", "Compliance=50");
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), validation, true)
            .Should().BeFalse();
        validation.Should().Contain(result => result.MemberNames.Contains(nameof(value.RiskDimensions)) &&
            result.ErrorMessage!.Contains($"'{name}' is not supported", StringComparison.Ordinal));

        var governed = ProcurementConfigurationDecisionRegistry.Validate("DEC-011", 1,
            JsonSerializer.SerializeToElement(value, DecisionJsonOptions));
        governed.IsValid.Should().BeFalse();
        governed.CanonicalJson.Should().BeNull();
        governed.Errors.Should().Contain(message => message.Contains($"'{name}' is not supported", StringComparison.Ordinal));
        governed.Errors.Should().Contain(message => message.Contains("FinancialStability", StringComparison.Ordinal));
        value.RiskDimensions.Should().Equal($"{name}=50", "Compliance=50");
    }

    [Fact]
    public void Existing_performance_aliases_and_exact_weights_are_preserved_when_saved()
    {
        var names = new[] { " performance = 33.33", "OverallPerformance=33.33", "SUPPLIERPERFORMANCE=33.34" };
        var value = ValidRiskPolicy(names);
        var governed = ProcurementConfigurationDecisionRegistry.Validate("DEC-011", 1,
            JsonSerializer.SerializeToElement(value, DecisionJsonOptions));

        governed.IsValid.Should().BeTrue();
        using var saved = JsonDocument.Parse(governed.CanonicalJson!);
        saved.RootElement.GetProperty("riskDimensions").EnumerateArray()
            .Select(item => item.GetString()).Should().Equal(names);
    }

    [Theory]
    [InlineData("Delivery=0", "Quality=100")]
    [InlineData("Delivery=-1", "Quality=101")]
    [InlineData("Delivery=60", "Quality=30")]
    [InlineData("Delivery=50", "delivery=50")]
    public void Supported_dimension_catalog_does_not_weaken_weight_and_duplicate_validation(
        string first, string second)
    {
        var value = ValidRiskPolicy(first, second);
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), validation, true)
            .Should().BeFalse();
        validation.Should().Contain(result => result.MemberNames.Contains(nameof(value.RiskDimensions)));
    }

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

    private static readonly JsonSerializerOptions DecisionJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
    };

    internal static ProcurementSupplierRiskDecisionValueDto ValidRiskPolicy(params string[] dimensions) => new()
    {
        EffectiveFrom = DateTime.UtcNow.Date,
        ReviewFrequencyMonths = 12,
        ExposureWindowMonths = 12,
        RiskDimensions = dimensions.ToList(),
        RiskBands = ["High=0-50", "Low=50-100"],
        ConcentrationLimitPercent = 35,
        MinimumScore = 70,
        EligibilityAction = ProcurementSupplierRiskEligibilityAction.EscalationRequired,
        PerformanceWindowMonths = 12,
        PerformanceDimensions =
        [
            "DeliveryTimeliness=15", "GrnQuality=15", "RejectionRate=15",
            "PriceCompetitiveness=15", "Responsiveness=10", "ComplaintResolution=10", "ContractCompletion=20"
        ],
        PerformanceBands = ["Unsatisfactory=0-50", "ImprovementRequired=50-75", "Satisfactory=75-100"],
        MinimumPerformanceDataCoveragePercent = 60,
        ResponseTargetHours = 48,
        PerformanceEligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop
    };
}
