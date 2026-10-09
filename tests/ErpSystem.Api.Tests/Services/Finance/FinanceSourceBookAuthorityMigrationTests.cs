using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;
using Xunit;
using AlignSourceBookAuthorityJournalOriginMigration = ErpSystem.Data.Migrations.AlignSourceBookAuthorityJournalOrigin;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceBookAuthorityMigrationTests
{
    [Fact]
    public void JournalOriginAlignmentMigration_UsesExplicitOriginWithLegacyFallback_AndGuardsDown()
    {
        var source = ReadMigration("src/ErpSystem.Data/Migrations/20261009163000_AlignSourceBookAuthorityJournalOrigin.cs");

        source.Should().Contain("Migration(\"20261009163000_AlignSourceBookAuthorityJournalOrigin\")")
            .And.Contain("EvidenceTriggerSql(useExplicitJournalOrigin: true)")
            .And.Contain("EvidenceTriggerSql(useExplicitJournalOrigin: false)")
            .And.Contain("CREATE OR ALTER TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_Evidence]")
            .And.Contain("NULLIF(UPPER(LTRIM(RTRIM(j.OriginModuleCode))),N'')")
            .And.Contain("CASE UPPER(LTRIM(RTRIM(j.SourceModule)))")
            .And.Contain("SOURCE_BOOK_AUTHORITY_POSTING_EVIDENCE_MISMATCH")
            .And.Contain("SOURCE_BOOK_AUTHORITY_JOURNAL_ORIGIN_DOWN_BLOCKED");
        source.Should().Contain("AND (CASE UPPER(LTRIM(RTRIM(j.SourceModule)))")
            .And.Contain("<>a.OriginModuleCode COLLATE Latin1_General_100_BIN2");

        var migration = new AlignSourceBookAuthorityJournalOriginMigration();
        var upBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeMigration(migration, "Up", upBuilder);
        var upSql = upBuilder.Operations.Should().ContainSingle().Which.Should().BeOfType<SqlOperation>().Which.Sql;
        upSql.Should().Contain("CREATE OR ALTER TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_Evidence]")
            .And.Contain("COALESCE(NULLIF(UPPER(LTRIM(RTRIM(j.OriginModuleCode))),N'')")
            .And.Contain("CASE UPPER(LTRIM(RTRIM(j.SourceModule)))");

        var downBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeMigration(migration, "Down", downBuilder);
        downBuilder.Operations.Should().HaveCount(2).And.OnlyContain(operation => operation is SqlOperation);
        var downSql = downBuilder.Operations.Cast<SqlOperation>().Select(operation => operation.Sql).ToArray();
        downSql[0].Should().Contain("SOURCE_BOOK_AUTHORITY_JOURNAL_ORIGIN_DOWN_BLOCKED")
            .And.Contain("CASE UPPER(LTRIM(RTRIM(j.SourceModule)))")
            .And.NotContain("j.OriginModuleCode");
        downSql[1].Should().Contain("CREATE OR ALTER TRIGGER [dbo].[TR_FinanceSourceBookAuthorities_Evidence]")
            .And.Contain("CASE UPPER(LTRIM(RTRIM(j.SourceModule)))")
            .And.NotContain("j.OriginModuleCode");
    }

    [Fact]
    public void CallerBindingMigration_BackfillsWorkflowType_AndAddsTenantExactLinksWithGuardedDown()
    {
        var source = ReadMigration("src/ErpSystem.Data/Migrations/20260930000500_AddFinanceSourceBookAuthorityCallerBindings.cs");
        source.Should().Contain("Migration(\"20260930000500_AddFinanceSourceBookAuthorityCallerBindings\")")
            .And.Contain("SourceWorkflowEntityType")
            .And.Contain("SET [SourceWorkflowEntityType]=[SourceDocumentType]")
            .And.Contain("HASHBYTES('SHA2_256'")
            .And.Contain("i.[SourceWorkflowEntityType]")
            .And.Contain("AddCallerBinding(migrationBuilder, \"Invoices\")")
            .And.Contain("AddCallerBinding(migrationBuilder, \"CustomerPayment\")")
            .And.Contain("AddCallerBinding(migrationBuilder, \"CashTransaction\")")
            .And.Contain("AddCallerBinding(migrationBuilder, \"VendorInvoice\")")
            .And.Contain("principalColumns: new[] { \"TenantId\", \"Id\" }")
            .And.Contain("ReferentialAction.Restrict")
            .And.Contain("SOURCE_BOOK_AUTHORITY_CALLER_DOWN_BLOCKED")
            .And.Contain("SOURCE_BOOK_AUTHORITY_WORKFLOW_TYPE_DOWN_BLOCKED");
        source.Should().Contain("initiatorApproval.InitiatorApproved");
        source.Should().NotContain("AK_WorkflowInstances_TenantId_Id")
            .And.NotContain("SUM(CASE WHEN a.ProcessedById=w.InitiatedById");
    }

    [Fact]
    public void Model_ExposesTenantExactKeys_OneWayEvidence_AndAppendOnlyOrigins()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SourceBookAuthorityModel;Trusted_Connection=True")
            .Options);
        var authority = db.Model.FindEntityType(typeof(FinanceSourceBookAuthority));
        var origin = db.Model.FindEntityType(typeof(FinanceSourceBookAuthorityOrigin));
        authority.Should().NotBeNull();
        origin.Should().NotBeNull();
        authority!.GetKeys().Should().Contain(key => key.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { nameof(FinanceSourceBookAuthority.TenantId), nameof(FinanceSourceBookAuthority.Id) }));
        authority.GetIndexes().Should().Contain(index => index.IsUnique && index.GetFilter() == "[OriginalJournalEntryId] IS NOT NULL");
        authority.GetIndexes().Should().Contain(index => index.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { nameof(FinanceSourceBookAuthority.TenantId), nameof(FinanceSourceBookAuthority.SourceWorkflowInstanceId) }));
        authority.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.Properties.Select(item => item.Name).SequenceEqual(new[]
            {
                nameof(FinanceSourceBookAuthority.TenantId), nameof(FinanceSourceBookAuthority.SourceWorkflowInstanceId)
            }) && foreignKey.PrincipalKey.Properties.Select(item => item.Name).SequenceEqual(new[]
            {
                nameof(FinanceSourceBookAuthority.TenantId), nameof(FinanceSourceBookAuthority.Id)
            }));
        authority.GetDeclaredTriggers().Select(item => item.ModelName).Should().Contain([
            "TR_FinanceSourceBookAuthorities_ImmutableBinding",
            "TR_FinanceSourceBookAuthorities_NoDelete",
            "TR_FinanceSourceBookAuthorities_Evidence"]);
        origin!.GetDeclaredTriggers().Select(item => item.ModelName).Should().Contain([
            "TR_FinanceSourceBookAuthorityOrigins_AppendOnly",
            "TR_FinanceSourceBookAuthorityOrigins_Evidence"]);
    }

    [Fact]
    public void Migration_HasNoBackfill_AndGuardsTenantEvidenceMutationAndDown()
    {
        var source = ReadMigration("src/ErpSystem.Data/Migrations/20260930000400_FinanceSourceBookAuthority.cs");
        source.Should().Contain("Migration(\"20260930000400_FinanceSourceBookAuthority\")")
            .And.Contain("AK_FinanceSourceBookAuthorities_TenantId_Id")
            .And.Contain("IX_FinanceSourceBookAuthorities_SourceVersion")
            .And.Contain("IX_FinanceSourceBookAuthorities_SourceWorkflow")
            .And.Contain("FK_FinanceSourceBookAuthorities_WorkflowInstances_TenantId_SourceWorkflowInstanceId")
            .And.Contain("IX_FinanceSourceBookAuthorities_Tenant_Workflow")
            .And.Contain("CK_FinanceSourceBookAuthorities_Lineage")
            .And.Contain("TR_FinanceSourceBookAuthorities_ImmutableBinding")
            .And.Contain("TR_FinanceSourceBookAuthorities_Evidence")
            .And.Contain("[dbo].[WorkflowStepInstances]")
            .And.Contain("[dbo].[WorkflowApprovals]")
            .And.Contain("finalApproval.FinalApprovalCount<>1")
            .And.Contain("initiatorApproval.InitiatorApproved")
            .And.Contain("a.ProcessedDate<=w.CompletedDate")
            .And.Contain("TR_FinanceSourceBookAuthorityOrigins_AppendOnly")
            .And.Contain("TR_FinanceSourceBookAuthorityOrigins_Evidence")
            .And.Contain("SOURCE_BOOK_AUTHORITY_WORKFLOW_EVIDENCE_MISMATCH")
            .And.Contain("SOURCE_BOOK_AUTHORITY_SUPERSESSION_INVALID")
            .And.Contain("SOURCE_BOOK_AUTHORITY_POSTING_EVIDENCE_MISMATCH")
            .And.Contain("j.ReversalJournalEntryId IS NOT NULL")
            .And.Contain("CAST(f.PostingDate AS date)<>i.EffectiveDate")
            .And.Contain("SOURCE_BOOK_AUTHORITY_DOWN_BLOCKED");
        source.Should().NotContain("UPDATE [dbo].[FinanceSourceBookAuthorities]")
            .And.NotContain("INSERT INTO [dbo].[FinanceSourceBookAuthorities]")
            .And.NotContain("SUM(CASE WHEN a.ProcessedById=w.InitiatedById")
            .And.NotContain("AK_WorkflowInstances_TenantId_Id");
    }

    private static string ReadMigration(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        throw new FileNotFoundException(relative);
    }

    private static void InvokeMigration(Migration migration, string methodName, MigrationBuilder builder)
    {
        var method = migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        method!.Invoke(migration, [builder]);
    }
}
