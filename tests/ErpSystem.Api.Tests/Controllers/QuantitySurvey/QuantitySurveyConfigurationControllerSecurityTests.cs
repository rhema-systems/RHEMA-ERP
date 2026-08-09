using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyConfigurationControllerSecurityTests
{
    [Fact]
    public void ControllerIsAuthenticatedAndEveryHttpActionHasAnExplicitPermissionPolicy()
    {
        var type = typeof(QuantitySurveyConfigurationProfilesController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();

        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToArray();

        actions.Should().NotBeEmpty();
        actions.Select(method => method.GetCustomAttribute<AuthorizeAttribute>()?.Policy)
            .Should().OnlyContain(policy => !string.IsNullOrWhiteSpace(policy));
    }

    [Theory]
    [InlineData(nameof(QuantitySurveyConfigurationProfilesController.Get), QuantitySurveyAccessControlRegistry.Read)]
    [InlineData(nameof(QuantitySurveyConfigurationProfilesController.Create), QuantitySurveyAccessControlRegistry.Manage)]
    [InlineData(nameof(QuantitySurveyConfigurationProfilesController.Approve), QuantitySurveyAccessControlRegistry.Approve)]
    [InlineData(nameof(QuantitySurveyConfigurationProfilesController.Publish), QuantitySurveyAccessControlRegistry.Approve)]
    [InlineData(nameof(QuantitySurveyConfigurationProfilesController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void SensitiveRoutesUseTheExpectedCentralPermission(string action, string permission)
    {
        typeof(QuantitySurveyConfigurationProfilesController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }
}
