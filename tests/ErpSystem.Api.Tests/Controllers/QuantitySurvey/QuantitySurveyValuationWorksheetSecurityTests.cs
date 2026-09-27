using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyValuationWorksheetSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.Search), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.Save), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.Vet), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.SubmitApproval), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.AddEvidence), QuantitySurveyAccessControlRegistry.ValuationsManage)]
    [InlineData(nameof(QuantitySurveyValuationWorksheetsController.OpenEvidence), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyValuationWorksheetsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_returns_safe_problem_details()
    {
        typeof(QuantitySurveyValuationWorksheetsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyValuationWorksheetsController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/valuation-worksheets");
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyValuationWorksheetsController.cs"));
        source.Should().Contain("correlationId");
        source.Should().NotContain("exception.ToString()");
        source.Should().NotContain("catch (Exception");
    }

    [Fact]
    public void External_routes_require_authentication_and_delegate_partner_scope_to_the_shared_service()
    {
        typeof(ExternalProjectValuationWorksheetsController).GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull();
        typeof(ExternalProjectValuationWorksheetsController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/projects/external/my-projects/{projectId:guid}/valuation-worksheets");
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyValuationWorksheetsController.cs"));
        source.Should().Contain("service.GetExternalAsync");
        source.Should().Contain("correlationId");
        source.Should().NotContain("exception.ToString()");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
