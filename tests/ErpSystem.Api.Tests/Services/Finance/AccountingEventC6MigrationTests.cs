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
        var operations = Migration().UpOperations();
        operations.First().Should().BeOfType<SqlOperation>().Which.Sql.Should().Contain("C6_SCHEMA_PREFLIGHT");
        operations.TakeWhile(item => item is SqlOperation).Should().ContainSingle();
        operations.OfType<CreateTableOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "AccountingEvents", "AccountingEventPostings", "AccountingEventAttempts");
    }

    [Fact]
    public void UpCarriesTenantSelectionVersionBookIdempotencyAndFailureAuthority()
    {
        var operations = Migration().UpOperations();
        var sql = string.Join("\n", operations.OfType<SqlOperation>().Select(item => item.Sql));
        sql.Should().Contain("TR_AccountingEvents_C6Authority").And.Contain("TR_AccountingEventPostings_C6Authority")
            .And.Contain("TR_AccountingEventAttempts_C6AppendOnly").And.Contain("C6_EVENT_LINEAGE")
            .And.Contain("C6_EVENT_SELECTION").And.Contain("C6_EVENT_RELEASE").And.Contain("C6_POSTING_RESULT");
        sql.Should().Contain("i.[AccountingBookSelectionEvidenceId]<>p.[AccountingBookSelectionEvidenceId]")
            .And.Contain("i.[EventKind]=N'Original' AND CONVERT(date,e.[EffectiveDate])<>i.[EventDate]")
            .And.Contain("i.[SourceDocumentId]<>p.[SourceDocumentId]")
            .And.Contain("C6_EVENT_OUTCOME_IMMUTABLE")
            .And.Contain("C6_ATTEMPT_FINAL_REQUIRED")
            .And.Contain("f.[SourceDocumentId]<>e.[SourceDocumentId]")
            .And.Contain("DATALENGTH(i.[PostingAction])<>DATALENGTH(p.[PostingAction])")
            .And.Contain("DATALENGTH(f.[PostingAction])<>DATALENGTH(e.[PostingAction])")
            .And.Contain("e.[EventKind]=N'Correction'")
            .And.Contain("N'AccountingEventCorrection'")
            .And.Contain("e.[EventKind]=N'Reversal'")
            .And.Contain("predecessor.[FinancePostingEventId]")
            .And.Contain("N'FinancePostingEventReversal'");
        var events = operations.OfType<CreateTableOperation>().Single(item => item.Name == "AccountingEvents");
        events.UniqueConstraints.Should().Contain(item => item.Columns.SequenceEqual(new[] { "TenantId", "Id", "Version" }));
        var postings = operations.OfType<CreateTableOperation>().Single(item => item.Name == "AccountingEventPostings");
        postings.ForeignKeys.Should().Contain(item => item.Columns.SequenceEqual(new[] { "TenantId", "AccountingEventId", "EventVersion" })
            && item.PrincipalColumns.SequenceEqual(new[] { "TenantId", "Id", "Version" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Table == "AccountingEvents" && item.Columns.SequenceEqual(new[] { "TenantId", "IdempotencyKey" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Table == "AccountingEvents" && item.Columns.SequenceEqual(new[] { "TenantId", "RootAccountingEventId", "Version" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique && item.Table == "AccountingEvents"
            && item.Columns.SequenceEqual(new[] { "TenantId", "OriginatingModuleCode", "SourceDocumentType", "SourceDocumentId", "PostingAction", "Version" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Table == "AccountingEventPostings" && item.Columns.SequenceEqual(new[] { "TenantId", "AccountingEventId", "EventVersion", "AccountingBookId" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Filter == "[FinancePostingEventId] IS NOT NULL" && item.Table == "AccountingEventPostings"
            && item.Columns.SequenceEqual(new[] { "TenantId", "FinancePostingEventId" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Filter == "[JournalEntryId] IS NOT NULL" && item.Table == "AccountingEventPostings"
            && item.Columns.SequenceEqual(new[] { "TenantId", "JournalEntryId" }));
        operations.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Table == "AccountingEventAttempts" && item.Columns.SequenceEqual(new[] { "TenantId", "AccountingEventId", "AttemptNumber" }));
    }

    [Fact]
    public void DownRefusesLossBeforeDroppingC6Schema()
    {
        var operations = Migration().DownOperations();
        operations.First().Should().BeOfType<SqlOperation>().Which.Sql.Should().Contain("C6_DOWN_BLOCKED");
        operations.OfType<DropTableOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "AccountingEvents", "AccountingEventPostings", "AccountingEventAttempts");
    }

    [Fact]
    public void MigrationRetainsExecutableDiscoveryMetadataWithoutDesigner()
    {
        typeof(AddAccountingEventOrchestrationFoundation).GetCustomAttributes(false).Select(item => item.GetType().Name)
            .Should().Contain(["DbContextAttribute", "MigrationAttribute"]);
    }

    private static ExposedMigration Migration() => new();

    private sealed class ExposedMigration : AddAccountingEventOrchestrationFoundation
    {
        public IReadOnlyList<MigrationOperation> UpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> DownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }
}
