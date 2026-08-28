using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyDesignRevisionImpactSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.Get), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.Create), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.Submit), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyDesignRevisionImpactsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Routes_require_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyDesignRevisionImpactsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Controller_is_authenticated_and_returns_safe_problem_details()
    {
        typeof(QuantitySurveyDesignRevisionImpactsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyDesignRevisionImpactsController.cs"));
        source.Should().Contain("correlationId");
        source.Should().Contain("QS_DESIGN_IMPACT_");
        source.Should().NotContain("catch (Exception");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
