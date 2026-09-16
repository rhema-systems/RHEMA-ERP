using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingEventC6MigrationTests
{
    [Fact]
    public void UpStartsWithFailBeforeMutationPreflight()
    {
        var source = Source();
        source.Should().Contain("C6_SCHEMA_PREFLIGHT");
        source.IndexOf("C6_SCHEMA_PREFLIGHT", StringComparison.Ordinal).Should().BeLessThan(
            source.IndexOf("migrationBuilder.CreateTable", StringComparison.Ordinal));
        foreach (var table in new[] { "AccountingEvents", "AccountingEventPostings", "AccountingEventAttempts" })
            source.Should().Contain($"name: \"{table}\"");
    }

    [Fact]
    public void UpCarriesTenantSelectionVersionBookIdempotencyAndFailureAuthority()
    {
        var sql = Source();
        sql.Should().Contain("TR_AccountingEvents_C6Authority").And.Contain("TR_AccountingEventPostings_C6Authority")
            .And.Contain("TR_AccountingEventAttempts_C6AppendOnly").And.Contain("C6_EVENT_LINEAGE")
            .And.Contain("C6_EVENT_SELECTION").And.Contain("C6_EVENT_RELEASE").And.Contain("C6_POSTING_RESULT");
        sql.Should().Contain("i.[AccountingBookSelectionEvidenceId]<>p.[AccountingBookSelectionEvidenceId]")
            .And.Contain("i.[EventKind]=N'Original' AND CONVERT(date,e.[EffectiveDate])<>i.[EventDate]")
            .And.Contain("i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2<>p.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2")
            .And.Contain("i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2<>p.[SourceDocumentType] COLLATE Latin1_General_100_BIN2")
            .And.Contain("i.[SourceDocumentId]<>p.[SourceDocumentId]")
            .And.Contain("i.[PostingAction] COLLATE Latin1_General_100_BIN2<>p.[PostingAction] COLLATE Latin1_General_100_BIN2")
            .And.Contain("C6_EVENT_OUTCOME_IMMUTABLE")
            .And.Contain("C6_ATTEMPT_FINAL_REQUIRED")
            .And.Contain("only a Posted exact-book representation may own leaf result evidence")
            .And.Contain("i.[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'FIN',N'INV',N'PROC',N'SALES',N'HR',N'QS',N'ESTATE',N'LEGAL',N'MAINT')")
            .And.Contain("LEFT(i.[SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'")
            .And.Contain("i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'")
            .And.Contain("i.[SourceDocumentType] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')")
            .And.Contain("LEFT(i.[PostingAction],1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[A-Z]'")
            .And.Contain("i.[PostingAction] COLLATE Latin1_General_100_BIN2 LIKE N'%[^A-Z0-9_.-]%'")
            .And.Contain("i.[PostingAction] COLLATE Latin1_General_100_BIN2 IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')")
            .And.Contain("f.[SourceDocumentId]<>e.[SourceDocumentId]")
            .And.Contain("DATALENGTH(i.[PostingAction])<>DATALENGTH(p.[PostingAction])")
            .And.Contain("DATALENGTH(f.[PostingAction])<>DATALENGTH(e.[PostingAction])")
            .And.Contain("e.[EventKind]=N'Correction'")
            .And.Contain("N'AccountingEventCorrection'")
            .And.Contain("e.[EventKind]=N'Reversal'")
            .And.Contain("predecessor.[FinancePostingEventId]")
            .And.Contain("N'FinancePostingEventReversal'");
        foreach (var token in new[]
        {
            "CK_AccountingEventPostings_ResultShape", "IX_AccountingEvents_TenantId_IdempotencyKey",
            "IX_AccountingEvents_TenantId_RootAccountingEventId_Version",
            "IX_AccountingEvents_TenantId_OriginatingModuleCode_SourceDocumentType_SourceDocumentId_PostingAction_Version",
            "IX_AccountingEventPostings_TenantId_AccountingEventId_EventVersion_AccountingBookId",
            "IX_AccountingEventPostings_TenantId_FinancePostingEventId",
            "IX_AccountingEventPostings_TenantId_JournalEntryId",
            "IX_AccountingEventAttempts_TenantId_AccountingEventId_AttemptNumber"
        }) sql.Should().Contain(token);
    }

    [Fact]
    public void DownRefusesLossBeforeDroppingC6Schema()
    {
        var source = Source();
        source.Should().Contain("C6_DOWN_BLOCKED");
        foreach (var table in new[] { "AccountingEvents", "AccountingEventPostings", "AccountingEventAttempts" })
            source.Should().Contain($"migrationBuilder.DropTable(\n                name: \"{table}\"");
    }

    [Fact]
    public void MigrationRetainsExecutableDiscoveryMetadataWithoutDesigner()
    {
        Source().Should().Contain("Migration(\"20260907071922_AddAccountingEventOrchestrationFoundation\")");
        typeof(DisposableDevelopmentCurrentModelBaseline).GetCustomAttributes(false).Select(item => item.GetType().Name)
            .Should().Contain(["DbContextAttribute", "MigrationAttribute"]);
    }

    private static string Source() => ArchivedMigrationSource.Read(
        "20260907071922_AddAccountingEventOrchestrationFoundation.cs");
}
