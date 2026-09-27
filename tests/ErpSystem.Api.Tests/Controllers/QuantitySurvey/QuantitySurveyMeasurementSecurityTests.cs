using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyMeasurementSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyMeasurementsController.Search), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.Create), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.Update), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.Record), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.AddAttachment), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.OpenAttachment), QuantitySurveyAccessControlRegistry.AuditRead)]
    [InlineData(nameof(QuantitySurveyMeasurementsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyMeasurementsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_uses_safe_problem_details()
    {
        typeof(QuantitySurveyMeasurementsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyMeasurementsController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/measurements");
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyMeasurementsController.cs"));
        source.Should().Contain("correlationId");
        source.Should().Contain("ControlledFileUploadException");
        source.Should().NotContain("exception.ToString()");
        source.Should().NotContain("catch (Exception");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
