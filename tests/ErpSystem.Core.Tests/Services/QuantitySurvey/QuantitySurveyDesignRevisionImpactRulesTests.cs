using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyDesignRevisionImpactRulesTests
{
    [Fact]
    public void Measurement_route_rejects_variation_only_impact()
    {
        var action = () => QuantitySurveyDesignRevisionImpactRules.ValidateLine(
            QuantitySurveyDesignImpactRoute.Measurement, QuantitySurveyDesignImpactType.ScopeAddition, 10, 12);
        action.Should().Throw<ArgumentException>().WithMessage("*only remeasurement or quantity-change impacts*");
    }

    [Fact]
    public void Variation_route_rejects_remeasurement_only_impact()
    {
        var action = () => QuantitySurveyDesignRevisionImpactRules.ValidateLine(
            QuantitySurveyDesignImpactRoute.Variation, QuantitySurveyDesignImpactType.RemeasurementRequired, 10, 12);
        action.Should().Throw<ArgumentException>().WithMessage("*measurement workflow route*");
    }

    [Theory]
    [InlineData(QuantitySurveyDesignImpactType.QuantityIncrease, 10, 11)]
    [InlineData(QuantitySurveyDesignImpactType.QuantityDecrease, 10, 9)]
    [InlineData(QuantitySurveyDesignImpactType.Omission, 10, 0)]
    public void Controlled_quantity_changes_accept_consistent_values(QuantitySurveyDesignImpactType type, decimal previous, decimal indicative)
    {
        var route = type == QuantitySurveyDesignImpactType.Omission
            ? QuantitySurveyDesignImpactRoute.Variation : QuantitySurveyDesignImpactRoute.Measurement;
        var action = () => QuantitySurveyDesignRevisionImpactRules.ValidateLine(route, type, previous, indicative);
        action.Should().NotThrow();
    }
}
