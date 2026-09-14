using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceProducerIntentC7MigrationTests
{
    [Fact]
    public void UpIsFailClosedAndAddsDecisionAndReceiptEvidence()
    {
        var sql = Source();
        sql.Should().Contain("C7_PREFLIGHT");
        foreach (var column in new[] { "ProducerDecisionStatus", "ProducerParticipantIdentity", "ProducerDecidedByUserId",
            "ProducerDecidedAtUtc", "ProducerDecisionReason", "ProducerIntentSnapshotJson", "ProducerIntentSnapshotHash" })
            sql.Should().Contain($"name: \"{column}\"");
        sql.Should().Contain("name: \"AccountingEventProducerReceipts\"");
        sql.Should().Contain("TR_AccountingEvents_C7ProducerDecision")
            .And.Contain("C7_MAKER_CHECKER").And.Contain("C7_DECISION_IMMUTABLE")
            .And.Contain("C7_EXECUTION_GATE").And.Contain("C7_INSERT_STATE")
            .And.Contain("C7_DECISION_ONLY").And.Contain("C7_RECEIPT_IMMUTABLE")
            .And.Contain("C7_RECEIPT_AUTHORITY").And.Contain("Rejected");
    }

    [Fact]
    public void DownRefusesEvidenceLossBeforeColumnsAreRemoved()
    {
        var source = Source();
        source.Should().Contain("C7_DOWN_REFUSED")
            .And.Contain("migrationBuilder.DropTable(")
            .And.Contain("AccountingEventProducerReceipts");
        foreach (var column in new[] { "ProducerDecisionStatus", "ProducerParticipantIdentity", "ProducerDecidedByUserId",
            "ProducerDecidedAtUtc", "ProducerDecisionReason", "ProducerIntentSnapshotJson", "ProducerIntentSnapshotHash" })
            source.Should().Contain(column);
    }

    [Fact]
    public void MigrationHasExecutableDiscoveryMetadata()
    {
        Source().Should().Contain("Migration(\"20260907190000_AddProducerIntentStagingC7\")");
        typeof(DisposableDevelopmentCurrentModelBaseline).GetCustomAttributes(false).Select(item => item.GetType().Name)
            .Should().Contain(["DbContextAttribute", "MigrationAttribute"]);
    }

    private static string Source() => ArchivedMigrationSource.Read("20260907190000_AddProducerIntentStagingC7.cs");
}
