using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringDevelopmentApprovalFileMigrationGuardTests
{
    [Fact]
    public void Migration_preserves_tenant_dms_configuration_and_append_only_controls()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260821110000_AddCivilEngineeringDevelopmentApprovalFiles.cs"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "FastBuildMigrationMetadata.cs"));

        sql.Should().Contain("ProjectCivilDevelopmentApprovalFiles")
            .And.Contain("ProjectCivilDevelopmentApprovalEvidence")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalFiles_Lineage")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalFiles_Lifecycle")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalEvidence_AppendOnly")
            .And.Contain("TR_ProjectCivilDevelopmentApprovalFileRevisions_AppendOnly")
            .And.Contain("PROJECT_PERMITTING_REVIEW")
            .And.Contain("CIV-CFG-009")
            .And.Contain("CIV-CFG-004")
            .And.Contain("CurrentVersion=version.VersionNumber")
            .And.Contain("OPENJSON(documentPolicy.ValueJson,'$.allowedFileExtensions')")
            .And.Contain("version.FileSize")
            .And.Contain("ProjectCivilDevelopmentApprovalFiles approvalFile")
            .And.NotContain("ProjectCivilDevelopmentApprovalFiles file ON");
        metadata.Should().Contain("20260821110000_AddCivilEngineeringDevelopmentApprovalFiles");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
