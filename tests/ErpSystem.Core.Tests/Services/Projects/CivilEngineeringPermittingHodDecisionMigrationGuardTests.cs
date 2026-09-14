using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringPermittingHodDecisionMigrationGuardTests
{
    [Fact]
    public void Migration_enforces_hod_handoff_workflow_projection_and_append_only_decision_controls()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "20260821153000_AddCivilEngineeringPermittingHodDecisions.cs"));

        sql.Should().Contain("TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Lineage")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_AppendOnly")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalEngineeringReviews_HodDecisionProjection")
            .And.Contain("TDC_HEAD_OF_CIVIL_ENGINEERING")
            .And.Contain("ProjectCivilDevelopmentApprovalFileHandoffs")
            .And.Contain("WorkflowOutcome='Approved'")
            .And.Contain("WorkflowOutcome='Rejected'")
            .And.Contain("ProjectCivilDevelopmentApprovalFiles approvalFile")
            .And.NotContain("ProjectCivilDevelopmentApprovalFiles file ON");
        sql.Should().Contain("[Migration(\"20260821153000_AddCivilEngineeringPermittingHodDecisions\")]");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
