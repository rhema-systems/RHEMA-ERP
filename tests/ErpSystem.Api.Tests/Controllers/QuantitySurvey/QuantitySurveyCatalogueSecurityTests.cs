using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyCatalogueSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyCataloguesController.List), QuantitySurveyAccessControlRegistry.Read)]
    [InlineData(nameof(QuantitySurveyCataloguesController.UnitOfMeasureOptions), QuantitySurveyAccessControlRegistry.Read)]
    [InlineData(nameof(QuantitySurveyCataloguesController.Create), QuantitySurveyAccessControlRegistry.Manage)]
    [InlineData(nameof(QuantitySurveyCataloguesController.Update), QuantitySurveyAccessControlRegistry.Manage)]
    [InlineData(nameof(QuantitySurveyCataloguesController.Delete), QuantitySurveyAccessControlRegistry.Manage)]
    public void RoutesRequireTheExpectedCentralPermission(string action, string permission)
    {
        typeof(QuantitySurveyCataloguesController)
            .GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(permission);
    }
}
