using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringRfiMigrationGuardTests
{
    [Fact]
    public void Migration_uses_authoritative_project_rfi_dms_and_append_only_controls()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", "20260820190000_AddCivilEngineeringRfiRouting.cs"));
        source.Should().Contain("ProjectRfis")
            .And.Contain("ProjectCivilRfiRoutings")
            .And.Contain("CentralDocumentVersions")
            .And.Contain("TR_ProjectCivilRfiResponses_AppendOnly")
            .And.Contain("ProjectExternalAccessPolicies")
            .And.NotContain("CREATE OR ALTER TRIGGER");
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
