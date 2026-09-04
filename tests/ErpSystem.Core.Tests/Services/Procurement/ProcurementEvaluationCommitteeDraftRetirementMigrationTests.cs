using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementEvaluationCommitteeDraftRetirementMigrationTests
{
    [Fact]
    public void ForwardMigrationPreservesRowsAndGuardsPristineDraftOnlyRetirement()
    {
        var operations = Operations("Up");
        var sql = string.Join(Environment.NewLine,
            operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        operations.OfType<AddColumnOperation>().Select(item => item.Name)
            .Should().BeEquivalentTo([
                "RetiredAtUtc", "RetiredByUserId", "RetirementReason",
                "RetirementEvidenceReference", "RetirementIdempotencyKey"
            ]);
        operations.Should().NotContain(operation => operation is DropTableOperation,
            "retirement must retain the control and its child history");
        sql.Should().Contain("d.Status = 0 AND i.Status = 3")
            .And.Contain("TR_ProcurementEvaluationCommitteeControls_DraftRetirement")
            .And.Contain("a.Status <> 0")
            .And.Contain("ProcurementEvaluationConflictDeclarations")
            .And.Contain("ProcurementEvaluationMeetings")
            .And.Contain("ProcurementEvaluationScoreSheets")
            .And.Contain("i.WorkflowInstanceId IS NOT NULL")
            .And.Contain("CHARINDEX(N'TRIGGER', UPPER(@updated))")
            .And.Contain("@triggerIndex - @createIndex")
            .And.Contain("N'ALTER '")
            .And.Contain("retirement metadata is immutable");
    }

    private static IReadOnlyList<MigrationOperation> Operations(string methodName)
    {
        var migration = new AddEvaluationCommitteeDraftRetirement();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }
}
