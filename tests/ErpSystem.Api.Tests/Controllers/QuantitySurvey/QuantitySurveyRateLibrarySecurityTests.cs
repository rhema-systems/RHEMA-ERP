using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyRateLibrarySecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyRateLibraryController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.MarketSurveySources), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.Create), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.Update), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.CreateRate), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.PrepareMarketSurveyUpdate), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.UpdateRate), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.PublishRate), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.RetireRate), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyRateLibraryController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyRateLibraryController)
            .GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_tenant_authenticated_and_uses_the_shared_route_family()
    {
        typeof(QuantitySurveyRateLibraryController).GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull();
        typeof(QuantitySurveyRateLibraryController).GetCustomAttribute<RouteAttribute>()!
            .Template.Should().Be("api/quantity-survey/rate-library");
    }
}
