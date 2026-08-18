using System.Reflection;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyEscalationDisputeSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.CalculationLookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.Open), QuantitySurveyAccessControlRegistry.ClaimsManage)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.Respond), QuantitySurveyAccessControlRegistry.ClaimsManage)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.AddAttachment), QuantitySurveyAccessControlRegistry.ClaimsManage)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.Resolve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.OpenAttachment), QuantitySurveyAccessControlRegistry.AuditRead)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    [InlineData(nameof(QuantitySurveyEscalationDisputesController.AuditPack), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyEscalationDisputesController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_uses_the_shared_QS_route_family()
    {
        typeof(QuantitySurveyEscalationDisputesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(QuantitySurveyEscalationDisputesController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/quantity-survey/escalation-disputes");
    }

    [Fact]
    public void Generic_document_renderer_cannot_bypass_QS_audit_permission()
    {
        DocumentsController.QuantitySurveyDocumentPolicies
            .Should().ContainKey(DocumentTypes.QuantitySurveyEscalationDisputeAuditPack)
            .WhoseValue.Should().Be(QuantitySurveyAccessControlRegistry.AuditRead);
    }

    [Fact]
    public void Controller_uses_safe_problem_details_and_bubbles_unexpected_failures()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyEscalationDisputesController.cs"));
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
