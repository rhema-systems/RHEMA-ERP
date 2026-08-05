using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementInvoiceThreeWayMatchMigrationTests
{
    [Fact]
    public void MigrationAddsTenantToleranceAndImmutableEventLineage()
    {
        var operations = Operations();
        var columns = operations.OfType<AddColumnOperation>().Select(item => item.Name);

        columns.Should().Contain([
            "ApInvoicePriceTolerancePercent",
            "ApInvoiceQuantityTolerancePercent",
            "MatchingControlEventId",
            "MatchingSnapshotHash",
            "MatchingEvaluatedAtUtc",
            "MatchingPriceTolerancePercent",
            "MatchingQuantityTolerancePercent",
            "MatchExceptionControlEventId"
        ]);
        operations.OfType<AddForeignKeyOperation>().Should().HaveCount(2)
            .And.OnlyContain(item => item.PrincipalTable == "ProcurementControlEvents");
    }

    [Fact]
    public void InvoiceApprovalTriggerFailsClosedOnMissingStaleOrForgedEvidence()
    {
        var sql = Sql();

        sql.Should().Contain("TR_VendorInvoice_TDC0504MandatoryMatch");
        sql.Should().Contain("THROW 51601");
        sql.Should().Contain("THROW 51603");
        sql.Should().Contain("InvoiceThreeWayMatchEvaluated");
        sql.Should().Contain("evaluation.RuleCode = 'AP-002'");
        sql.Should().Contain("evaluation.RuleVersion = 'TDC-0504'");
        sql.Should().Contain("JSON_VALUE(evaluation.ResultValuesJson, '$.snapshotHash') <> invoice.MatchingSnapshotHash");
        sql.Should().Contain("ProcurementReceiptInspectionCases");
        sql.Should().Contain("ProcurementReceiptInspectionLines");
        Enumerable.Range(1, 14).Should().OnlyContain(number => sql.Contains($"'DEC-{number:000}'"));
    }

    [Fact]
    public void ExceptionConsumptionIsNarrowAndDoesNotCreateAParallelWorkflow()
    {
        var sql = Sql();

        sql.Should().Contain("InvoiceMatchExceptionApproved");
        sql.Should().Contain("exceptionEvent.RuleCode = 'AP-006'");
        sql.Should().Contain("exceptionEvent.RuleVersion = 'TDC-0507'");
        sql.Should().Contain("workflow.Status = 2");
        sql.Should().Contain("workflow.InitiatedById <> exceptionEvent.ActorUserId");
        sql.Should().Contain("ProcurementControlEventEvidenceLinks");
        sql.Should().NotContain("CREATE TABLE");
    }

    [Fact]
    public void SubmittedInvoiceLinesAreImmutableAndDraftChangesInvalidateMatch()
    {
        var sql = Sql();

        sql.Should().Contain("TR_VendorInvoiceLineItem_TDC0504MatchIntegrity");
        sql.Should().Contain("THROW 51602");
        sql.Should().Contain("MatchingControlEventId = NULL");
        sql.Should().Contain("MatchExceptionControlEventId = NULL");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0504MandatoryThreeWayMatch();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(
        Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
