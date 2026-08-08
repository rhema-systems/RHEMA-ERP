using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyBoqSpreadsheetSecurityTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndUsesProjectScopedRoute()
    {
        typeof(QuantitySurveyBoqSpreadsheetController)
            .GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull();

        typeof(QuantitySurveyBoqSpreadsheetController)
            .GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/projects/{projectId:guid}/boq-spreadsheet");
    }

    [Theory]
    [InlineData(nameof(QuantitySurveyBoqSpreadsheetController.Template), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(QuantitySurveyBoqSpreadsheetController.Preview), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(QuantitySurveyBoqSpreadsheetController.Commit), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(QuantitySurveyBoqSpreadsheetController.ValidationReport), QuantitySurveyAccessControlRegistry.BoqManage)]
    [InlineData(nameof(QuantitySurveyBoqSpreadsheetController.Export), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    public void RoutesRequireTheExpectedQuantitySurveyPermission(string action, string permission)
    {
        typeof(QuantitySurveyBoqSpreadsheetController)
            .GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(permission);
    }

    [Theory]
    [InlineData(4, "IF(OR(O4=\"\",P4=\"\"),\"\",O4*P4)")]
    [InlineData(32, "= IF(OR(O32 = \"\", P32 = \"\"), \"\", O32 * P32)")]
    public void ExpectedLockedTotalFormulaIsAccepted(int row, string formula)
    {
        QuantitySurveyBoqSpreadsheetService.IsExpectedTotalFormula(row, formula)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(4, "SUM(O4:P4)")]
    [InlineData(4, "IF(OR(O5=\"\",P5=\"\"),\"\",O5*P5)")]
    [InlineData(4, "WEBSERVICE(\"https://example.invalid\")")]
    [InlineData(4, "")]
    public void ChangedOrUnsafeTotalFormulaIsRejected(int row, string formula)
    {
        QuantitySurveyBoqSpreadsheetService.IsExpectedTotalFormula(row, formula)
            .Should().BeFalse();
    }
}
