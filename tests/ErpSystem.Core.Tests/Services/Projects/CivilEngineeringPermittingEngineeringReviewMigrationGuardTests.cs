using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringPermittingEngineeringReviewMigrationGuardTests
{
    [Fact]
    public void Migration_enforces_tenant_handoff_sce_policy_dms_and_append_only_review_controls()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821143000_AddCivilEngineeringPermittingEngineeringReviews.cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "FastBuildMigrationMetadata.cs"));

        sql.Should().Contain("TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lineage")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lifecycle")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_Lineage")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_AppendOnly")
            .And.Contain("PROJECT_PERMITTING_REVIEW")
            .And.Contain("TDC_SUPERVISING_CIVIL_ENGINEER")
            .And.Contain("OPENJSON(permitting.ValueJson,'$.commentCategoryIds')")
            .And.Contain("OPENJSON(permitting.ValueJson,'$.allowedOutcomes')")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("AFTER INSERT AS")
            .And.Contain("ProjectCivilDevelopmentApprovalFiles approvalFile")
            .And.NotContain("ProjectCivilDevelopmentApprovalFiles file ON");
        metadata.Should().Contain("20260821143000_AddCivilEngineeringPermittingEngineeringReviews");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
