using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceProducerIntentC7MigrationTests
{
    [Fact]
    public void UpIsFailClosedAndAddsOnlyDecisionEvidence()
    {
        var operations = Migration().UpOperations();
        operations.First().Should().BeOfType<SqlOperation>().Which.Sql.Should().Contain("C7_PREFLIGHT");
        operations.OfType<AddColumnOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "ProducerDecisionStatus", "ProducerParticipantIdentity", "ProducerDecidedByUserId",
            "ProducerDecidedAtUtc", "ProducerDecisionReason", "ProducerIntentSnapshotJson", "ProducerIntentSnapshotHash");
        var sql = string.Join('\n', operations.OfType<SqlOperation>().Select(item => item.Sql));
        sql.Should().Contain("TR_AccountingEvents_C7ProducerDecision")
            .And.Contain("C7_MAKER_CHECKER").And.Contain("C7_DECISION_IMMUTABLE")
            .And.Contain("C7_EXECUTION_GATE").And.Contain("C7_INSERT_STATE")
            .And.Contain("C7_DECISION_ONLY").And.Contain("Rejected");
    }

    [Fact]
    public void DownRefusesEvidenceLossBeforeColumnsAreRemoved()
    {
        var operations = Migration().DownOperations();
        operations.First().Should().BeOfType<SqlOperation>().Which.Sql.Should().Contain("C7_DOWN_REFUSED");
        operations.OfType<DropColumnOperation>().Should().HaveCount(7);
    }

    [Fact]
    public void MigrationHasExecutableDiscoveryMetadata()
    {
        typeof(AddProducerIntentStagingC7).GetCustomAttributes(false).Select(item => item.GetType().Name)
            .Should().Contain(["DbContextAttribute", "MigrationAttribute"]);
    }

    private static ExposedMigration Migration() => new();

    private sealed class ExposedMigration : AddProducerIntentStagingC7
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
