using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDevelopmentApprovalHandoffMigrationGuardTests
{
    [Fact]
    public void Migration_enforces_sequence_recipient_dms_and_append_only_handoff_controls()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821123000_AddCivilEngineeringDevelopmentApprovalFileHandoffs.cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "FastBuildMigrationMetadata.cs"));
        sql.Should().Contain("TR_ProjectCivilDevelopmentApprovalFileHandoffs_Lineage")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalFileHandoffs_AppendOnly")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalHandoffEvidence_Lineage")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalHandoffEvidence_AppendOnly")
            .And.Contain("OPENJSON(permitting.ValueJson,'$.handoffRoleIds')")
            .And.Contain("PROJECT_PERMITTING_REVIEW")
            .And.Contain("CK_ProjectCivilDevelopmentApprovalFileHandoffs_DueDate")
            .And.Contain("value.DueDate)>CONVERT(date,approvalFile.DueDate)")
            .And.Contain("CurrentVersion=version.VersionNumber")
            .And.Contain("ProjectCivilDevelopmentApprovalFiles approvalFile")
            .And.NotContain("ProjectCivilDevelopmentApprovalFiles file ON");
        metadata.Should().Contain("20260821123000_AddCivilEngineeringDevelopmentApprovalFileHandoffs");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
