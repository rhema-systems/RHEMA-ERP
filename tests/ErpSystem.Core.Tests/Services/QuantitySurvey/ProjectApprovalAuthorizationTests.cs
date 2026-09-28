using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class ProjectApprovalAuthorizationTests
{
    [Fact]
    public void ProjectApproval_AllowsConfiguredSupervisingQsApproverToReachWorkflowAssignmentGate()
    {
        var root = FindRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectService.Authorization.cs"));

        source.Should().Contain("ProjectAccessOperation.ApproveWorkflow => canView ||");
        source.Should().Contain("TDC_SUPERVISING_QUANTITY_SURVEYOR");
        var workflowSource = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Projects", "ProjectServices.cs"));
        workflowSource.Should().Contain("CanUserApproveAsync(\"Project\", id, userId)");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
