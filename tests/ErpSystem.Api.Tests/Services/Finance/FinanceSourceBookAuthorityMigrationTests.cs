using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceBookAuthorityMigrationTests
{
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
        var source = ReadMigration();
        source.Should().Contain("Migration(\"20260930000400_FinanceSourceBookAuthority\")")
            .And.Contain("AK_FinanceSourceBookAuthorities_TenantId_Id")
            .And.Contain("IX_FinanceSourceBookAuthorities_SourceVersion")
            .And.Contain("IX_FinanceSourceBookAuthorities_SourceWorkflow")
            .And.Contain("CK_FinanceSourceBookAuthorities_Lineage")
            .And.Contain("TR_FinanceSourceBookAuthorities_ImmutableBinding")
            .And.Contain("TR_FinanceSourceBookAuthorities_Evidence")
            .And.Contain("TR_FinanceSourceBookAuthorityOrigins_AppendOnly")
            .And.Contain("TR_FinanceSourceBookAuthorityOrigins_Evidence")
            .And.Contain("SOURCE_BOOK_AUTHORITY_WORKFLOW_EVIDENCE_MISMATCH")
            .And.Contain("SOURCE_BOOK_AUTHORITY_SUPERSESSION_INVALID")
            .And.Contain("SOURCE_BOOK_AUTHORITY_POSTING_EVIDENCE_MISMATCH")
            .And.Contain("j.ReversalJournalEntryId IS NOT NULL")
            .And.Contain("CAST(f.PostingDate AS date)<>i.EffectiveDate")
            .And.Contain("SOURCE_BOOK_AUTHORITY_DOWN_BLOCKED");
        source.Should().NotContain("UPDATE [dbo].[FinanceSourceBookAuthorities]")
            .And.NotContain("INSERT INTO [dbo].[FinanceSourceBookAuthorities]");
    }

    private static string ReadMigration()
    {
        const string relative = "src/ErpSystem.Data/Migrations/20260930000400_FinanceSourceBookAuthority.cs";
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        throw new FileNotFoundException(relative);
    }
}
