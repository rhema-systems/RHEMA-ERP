using System.Reflection;
using ErpSystem.Api.Controllers.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.QuantitySurvey;

public sealed class QuantitySurveyJointMeasurementSecurityTests
{
    [Theory]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.Lookups), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.List), QuantitySurveyAccessControlRegistry.WorkspaceRead)]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.Create), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.Schedule), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.SubmitForApproval), QuantitySurveyAccessControlRegistry.MeasurementsManage)]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.Approve), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.Reject), QuantitySurveyAccessControlRegistry.TransactionsApprove)]
    [InlineData(nameof(QuantitySurveyJointMeasurementsController.History), QuantitySurveyAccessControlRegistry.AuditRead)]
    public void Internal_routes_require_the_expected_central_permission(string action, string permission)
    {
        typeof(QuantitySurveyJointMeasurementsController).GetMethod(action)!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public void Both_surfaces_are_authenticated_and_return_safe_problem_details()
    {
        typeof(QuantitySurveyJointMeasurementsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ExternalProjectJointMeasurementsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Controllers",
            "QuantitySurvey", "QuantitySurveyJointMeasurementsController.cs"));
        source.Should().Contain("correlationId");
        source.Should().Contain("QS_JOINT_MEASUREMENT_");
        source.Should().NotContain("catch (Exception");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
