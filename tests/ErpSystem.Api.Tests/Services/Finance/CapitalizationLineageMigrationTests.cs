using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CapitalizationLineageMigrationTests
{
    [Fact]
    public void Migration006_retains_tenant_exact_lineage_and_refuses_evidence_loss()
    {
        var source = ReadMigration("src/ErpSystem.Data/Migrations/20260930000600_AddCapitalizationLineage.cs");
        source.Should().Contain("Migration(\"20260930000600_AddCapitalizationLineage\")")
            .And.Contain("FixedAssetCapitalizationCycles")
            .And.Contain("CapitalizationWorkflowInstanceId")
            .And.Contain("CapitalizationSourceBookAuthorityId")
            .And.Contain("SourceFinancePostingEventId")
            .And.Contain("SourceBookAuthorityId")
            .And.Contain("AddUniqueConstraint(")
            .And.Contain("name: \"AK_FixedAssets_TenantId_Id\"")
            .And.Contain("DropUniqueConstraint(\"AK_FixedAssets_TenantId_Id\"")
            .And.Contain("DropIndex(\"IX_CapitalProjects_TenantId\"")
            .And.Contain("DropIndex(\"IX_ProjectCostLines_TenantId\"")
            .And.Contain("CreateIndex(\"IX_CapitalProjects_TenantId\"")
            .And.Contain("CreateIndex(\"IX_ProjectCostLines_TenantId\"")
            .And.Contain("principalColumns: new[] { \"TenantId\", \"Id\" }")
            .And.Contain("CAPITALIZATION_LINEAGE_DOWN_BLOCKED")
            .And.Contain("SELECT 1 FROM [dbo].[FixedAssetCapitalizationCycles]")
            .And.Contain("d.WorkflowInstanceId IS NOT NULL AND (i.WorkflowInstanceId IS NULL")
            .And.Contain("d.SourceBookAuthorityId IS NOT NULL AND (i.SourceBookAuthorityId IS NULL")
            .And.Contain("d.OriginalFinancePostingEventId IS NOT NULL AND (i.OriginalFinancePostingEventId IS NULL")
            .And.Contain("d.ReversalFinancePostingEventId IS NOT NULL AND (i.ReversalFinancePostingEventId IS NULL");
        source.Should().NotContain("WHERE [IsDeleted] = 0");
    }

    [Fact]
    public void Runtime_model_exposes_cycle_concurrency_and_tenant_composite_evidence_links()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CapitalizationLineageModel;Trusted_Connection=True")
            .Options);
        var cycle = db.Model.FindEntityType(typeof(FixedAssetCapitalizationCycle));
        var fixedAsset = db.Model.FindEntityType(typeof(FixedAsset));
        var project = db.Model.FindEntityType(typeof(CapitalProject));
        var costLine = db.Model.FindEntityType(typeof(ProjectCostLine));
        cycle.Should().NotBeNull();
        fixedAsset!.GetKeys().Should().Contain(key => key.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { nameof(FixedAsset.TenantId), nameof(FixedAsset.Id) }));
        project!.GetIndexes().Should().NotContain(index => index.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { nameof(CapitalProject.TenantId) }));
        costLine!.GetIndexes().Should().NotContain(index => index.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { nameof(ProjectCostLine.TenantId) }));
        cycle!.FindProperty(nameof(FixedAssetCapitalizationCycle.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        cycle.GetForeignKeys().Should().Contain(foreignKey => foreignKey.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { nameof(FixedAssetCapitalizationCycle.TenantId), nameof(FixedAssetCapitalizationCycle.WorkflowInstanceId) }));
        cycle.GetForeignKeys().Should().Contain(foreignKey => foreignKey.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { nameof(FixedAssetCapitalizationCycle.TenantId), nameof(FixedAssetCapitalizationCycle.SourceBookAuthorityId) }));
        foreach (var propertyName in new[]
                 {
                     nameof(FixedAssetCapitalizationCycle.OriginalFinancePostingEventId),
                     nameof(FixedAssetCapitalizationCycle.OriginalJournalEntryId),
                     nameof(FixedAssetCapitalizationCycle.ReversalFinancePostingEventId),
                     nameof(FixedAssetCapitalizationCycle.ReversalJournalEntryId)
                 })
        {
            cycle.GetIndexes().Should().Contain(index => index.IsUnique &&
                index.GetFilter() == $"[{propertyName}] IS NOT NULL" &&
                index.Properties.Select(item => item.Name).SequenceEqual(new[]
                {
                    nameof(FixedAssetCapitalizationCycle.TenantId), propertyName
                }));
        }
        cycle.GetDeclaredTriggers().Select(item => item.ModelName)
            .Should().Contain("TR_FixedAssetCapitalizationCycles_Evidence");
    }

    [Fact]
    public void Direct_capitalization_requires_cycle_bound_approval_and_retains_posting_evidence()
    {
        var service = ReadMigration("src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs");
        service.Should().Contain("StartApprovalWorkflowAsync(\"FixedAsset\", cycle.Id)")
            .And.Contain("SourceDocumentId = cycle.Id")
            .And.Contain("cycle.SourceBookAuthorityId = authority.AuthorityId")
            .And.Contain("cycle.Status != \"Approved\"")
            .And.Contain("cycle.OriginalFinancePostingEventId = postingResult.PostingEventId")
            .And.Contain("cycle.OriginalJournalEntryId = postingResult.JournalEntryId")
            .And.Contain("cycle.ReversalFinancePostingEventId = posting.PostingEventId")
            .And.Contain("cycle.ReversalJournalEntryId = posting.JournalEntryId")
            .And.Contain("BindOriginalPostingAsync(");
        var approvals = ReadMigration("src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs");
        approvals.Should().Contain("cycle.WorkflowInstanceId != instance.Id")
            .And.Contain("cycle.Status = \"Approved\"")
            .And.Contain("cycle.Status = \"Rejected\"");
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
}
