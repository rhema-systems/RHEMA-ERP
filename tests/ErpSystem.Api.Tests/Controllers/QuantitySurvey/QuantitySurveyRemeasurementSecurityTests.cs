using System.Reflection;
using ErpSystem.Api.Controllers.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyRemeasurementSecurityTests
{
    [Theory]
    [InlineData(nameof(ProjectsController.GetProjectBoqRemeasurementWorkspace), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(ProjectsController.CreateProjectBoqRemeasurement), QuantitySurveyAccessControlRegistry.BoqManage)]
    public void Routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(ProjectsController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void New_routes_return_safe_correlation_bearing_problem_details()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "Projects", "ProjectsController.cs"));
        source.Should().Contain("quantity-survey-remeasurement-");
        source.Should().Contain("QS_REMEASUREMENT_");
        source.Should().Contain("correlationId");
        source.Should().Contain("ExecuteProjectRemeasurementAsync");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
