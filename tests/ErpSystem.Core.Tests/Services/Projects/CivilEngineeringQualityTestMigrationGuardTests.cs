using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringQualityTestMigrationGuardTests
{
    [Fact]
    public void Migration_adds_only_the_quality_test_register_and_immutable_sql_guards()
    {
        var root = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260820193000_AddCivilEngineeringQualityTestRegister.cs"));
        Assert.Contains("CREATE TABLE ProjectCivilQualityTestReports", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE ProjectCivilQualityTestRevisions", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilQualityTestReports_Lineage", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilQualityTestReports_Lifecycle", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilQualityTestRevisions_AppendOnly", sql, StringComparison.Ordinal);
        Assert.Contains("PROJECT_QUALITY_TEST", sql, StringComparison.Ordinal);
        Assert.Contains("OPENJSON(decision.ValueJson, '$.reviewerRoleIds')", sql, StringComparison.Ordinal);
        Assert.Contains("EndorsementDocumentVersionId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE Projects", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
