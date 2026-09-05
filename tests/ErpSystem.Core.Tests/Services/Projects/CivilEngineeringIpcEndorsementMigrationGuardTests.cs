using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringIpcEndorsementMigrationGuardTests
{
    [Fact]
    public void Migration_adds_only_the_ipc_review_overlay_with_tenant_dms_and_immutable_sql_guards()
    {
        var root = FindRepositoryRoot();
        var sql = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", "20260820233000_AddCivilEngineeringIpcEndorsements.cs"));
        Assert.Contains("CREATE TABLE ProjectCivilIpcEndorsements", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE ProjectCivilIpcEndorsementRevisions", sql, StringComparison.Ordinal);
        Assert.Contains("IF OBJECT_ID(N'dbo.ProjectCivilIpcEndorsements', N'U') IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("IF OBJECT_ID(N'dbo.ProjectCivilIpcEndorsementRevisions', N'U') IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilIpcEndorsements_Lineage", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilIpcEndorsements_Lifecycle", sql, StringComparison.Ordinal);
        Assert.Contains("TR_ProjectCivilIpcEndorsementRevisions_AppendOnly", sql, StringComparison.Ordinal);
        Assert.Equal(4, sql.Split("CREATE OR ALTER TRIGGER", StringSplitOptions.None).Length - 1);
        Assert.Contains("QS_PAYMENT_CERTIFICATE", sql, StringComparison.Ordinal);
        Assert.Contains("CIV-CFG-005", sql, StringComparison.Ordinal);
        Assert.Contains("CIV-CFG-004", sql, StringComparison.Ordinal);
        Assert.Contains("OPENJSON(supervision.ValueJson,'$.projectEngineerRoleIds')", sql, StringComparison.Ordinal);
        Assert.Contains("assignment.EffectiveFrom<=SYSUTCDATETIME()", sql, StringComparison.Ordinal);
        Assert.Contains("certificate.ApprovalWorkflowDefinitionId IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("value.EvidenceDocumentVersionId IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 52155, 'Civil IPC endorsement revisions are append-only.', 1", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE VendorInvoices", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
