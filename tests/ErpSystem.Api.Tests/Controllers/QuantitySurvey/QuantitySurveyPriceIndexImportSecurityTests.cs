using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyPriceIndexImportSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.Template), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.Stage), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.Submit), QuantitySurveyAccessControlRegistry.RatesManage)]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyPriceIndexImportsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyPriceIndexImportsController)
            .GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_uses_the_shared_QS_route_family()
    {
        typeof(QuantitySurveyPriceIndexImportsController)
            .GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyPriceIndexImportsController)
            .GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/price-index-imports");
    }

    [Fact]
    public void Controller_returns_safe_correlation_bearing_problem_details()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyPriceIndexImportsController.cs"));

        source.Should().Contain("StatusCodes.Status403Forbidden");
        source.Should().Contain("Price-index import access forbidden");
        source.Should().Contain("ControlledFileUploadException");
        source.Should().Contain("correlationId");
        source.Should().NotContain("exception.ToString()");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
